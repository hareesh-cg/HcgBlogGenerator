using System.Collections.Concurrent;
using System.Reflection;

using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Models;
using HcgBlogGenerator.Core.Utilities;

using Microsoft.Extensions.Logging;

using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Implements ITemplateEngine using the Scriban library.
/// Loads templates (layouts, includes) during initialization and renders them with data.
/// </summary>
public class ScribanTemplateEngine : ITemplateEngine {
    private readonly ILogger<ScribanTemplateEngine> _logger;
    private readonly ConcurrentDictionary<string, Template> _cachedTemplates = new();
    private IFileSystem? _templateFileSystem; // Filesystem scoped to the template directory
    private string _templateRootPath = string.Empty; // e.g., "layouts" or "templates"
    private string _includesRootPath = string.Empty; // Path to the "includes" directory

    public ScribanTemplateEngine(ILogger<ScribanTemplateEngine> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogDebug("ScribanTemplateEngine initialized.");
    }

    /// <summary>
    /// Loads and parses all templates from the specified directory using the provided filesystem.
    /// This prepares the engine for rendering requests.
    /// </summary>
    /// <param name="templateDirectory">Directory path relative to the source root containing templates (e.g., "layouts").</param>
    /// <param name="fileSystem">The filesystem instance (scoped to the site source) used to read templates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InitializeAsync(SiteConfiguration configuration, IFileSystem fileSystem, CancellationToken cancellationToken = default) {
        _templateRootPath = configuration.TemplateDirectory.Trim('/');
        _includesRootPath = configuration.IncludesDirectory.Trim('/');

        _logger.LogInformation("Initializing Scriban template engine. Loading templates from: {TemplateDirectory}", _templateRootPath);
        _cachedTemplates.Clear(); // Clear cache on re-initialization

        // Store the filesystem and root path for the custom loader
        // We assume the passed fileSystem operates relative to the site root,
        // so templateDirectory is a sub-path within that.
        _templateFileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

        var loadedTemplates = new Dictionary<string, Template>(); // Use temp dictionary to avoid concurrency issues during load
        int errorCount = 0;

        // --- Load Layouts ---
        _logger.LogDebug("Loading layout templates from: {Path}", _templateRootPath);
        await LoadTemplatesFromDirectoryAsync(_templateRootPath, _templateRootPath, fileSystem, loadedTemplates, cancellationToken)
              .ContinueWith(t => errorCount += t.Result, TaskContinuationOptions.OnlyOnRanToCompletion); // Count errors

        // --- Load Includes ---
        _logger.LogDebug("Loading include templates from: {Path}", _includesRootPath);
        await LoadTemplatesFromDirectoryAsync(_includesRootPath, _includesRootPath, fileSystem, loadedTemplates, cancellationToken)
              .ContinueWith(t => errorCount += t.Result, TaskContinuationOptions.OnlyOnRanToCompletion); // Count errors


        // --- Populate Concurrent Cache ---
        foreach (var kvp in loadedTemplates) {
            _cachedTemplates.TryAdd(kvp.Key, kvp.Value);
        }

        _logger.LogInformation("Template initialization complete. Loaded {LoadedCount} templates/includes, encountered {ErrorCount} errors.", _cachedTemplates.Count, errorCount);

        if (_cachedTemplates.Any()) {
            _logger.LogTrace("Final template cache keys: [{FinalKeys}]", string.Join(", ", _cachedTemplates.Keys));
        }
        else {
            _logger.LogWarning("Template cache is EMPTY after initialization.");
        }
    }

    private async Task<int> LoadTemplatesFromDirectoryAsync(
            string directoryPath,       // The directory to search (relative to source root)
            string basePathForKey,      // The base path to make keys relative to (e.g., "layouts" or "includes")
            IFileSystem fileSystem,
            Dictionary<string, Template> targetDictionary, // Temporary dictionary
            CancellationToken cancellationToken) {
        _logger.LogInformation("Attempting to load templates from directory path: '{DirectoryPath}'", directoryPath);
        int errors = 0;
        List<string> templateFiles;
        try {
            // Find .html or .scriban files recursively within the specific directory
            var templateFilesList = (await fileSystem.GetFilesAsync(directoryPath, "*.*", true, cancellationToken)).ToList();
            _logger.LogDebug("GetFilesAsync('{Dir}', '*.*', true) returned {Count} raw paths.", directoryPath, templateFilesList.Count);
            foreach (var f in templateFilesList) {
                _logger.LogTrace("- Raw path found: {FilePath}", f);
            } // Log each raw path

            templateFiles = templateFilesList.Where(f =>
                f.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".scriban", StringComparison.OrdinalIgnoreCase))
                .ToList();
            _logger.LogDebug("Filtered down to {Count} .html/.scriban files in '{Dir}'.", templateFiles.Count, directoryPath);
            foreach (var f in templateFiles) {
                _logger.LogTrace("- Filtered path: {FilePath}", f);
            } 
        }
        catch (DirectoryNotFoundException) {
            _logger.LogWarning("Template directory not found: {Dir}. Skipping load.", directoryPath);
            return 0; // Not an error, just no templates
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error discovering template files in {Dir}", directoryPath);
            return 1; // Indicate an error occurred during discovery
        }


        foreach (var filePath in templateFiles) // filePath is relative to source root (e.g., layouts/post.html)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // --- Calculate Cache Key ---
            // Key should be relative to the conceptual root for that type (layout or include)
            // E.g., "post.html" or "header.html" or "partials/nav.html"
            // We need Path.GetRelativePath functionality here.

            // Assume filePath and basePathForKey are structured like "folder/file.html" or "folder/"
            string templateKey = filePath;
            string basePrefix = basePathForKey.TrimEnd('/') + "/";
            if (filePath.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase)) {
                templateKey = filePath.Substring(basePrefix.Length);
            }
            else {
                // This case shouldn't happen if GetFilesAsync works correctly with the prefix, but log if it does
                _logger.LogWarning("Template file '{FilePath}' found outside its expected base path '{BasePath}'. Using full path as key might cause issues.", filePath, basePathForKey);
                // Use filename only as fallback?
                templateKey = Path.GetFileName(filePath);
            }

            // Normalize key
            templateKey = templateKey.Replace('\\', '/');
            _logger.LogTrace("Calculated cache key: '{TemplateKey}' for file: {FilePath}", templateKey, filePath); // Log calculated key

            try {
                _logger.LogTrace("Loading template file: {FilePath} with cache key: {TemplateKey}", filePath, templateKey);
                string templateContent = await fileSystem.ReadAllTextAsync(filePath, cancellationToken);

                _logger.LogTrace("Parsing template file content: {FilePath}", filePath);
                var template = Template.Parse(templateContent, filePath); // Use full path for errors

                if (template.HasErrors) {
                    errors++;
                    LogTemplateErrors(template.Messages, filePath);
                    _logger.LogError("Failed to parse template: {TemplateKey}", templateKey);
                    continue;
                }

                if (!targetDictionary.TryAdd(templateKey, template)) {
                    _logger.LogWarning("Duplicate template key detected: {TemplateKey} from file {FilePath}. Check directory structure.", templateKey, filePath);
                    // errors++; // Treat duplicates as errors? Or just warn? Warn for now.
                }
                else {
                    _logger.LogDebug("Successfully loaded and added template with key: {TemplateKey}", templateKey); // Log success
                }
            }
            catch (Exception ex) {
                errors++;
                _logger.LogError(ex, "Failed to load or parse template file: {FilePath}", filePath);
            }
        }
        return errors;
    }

    /// <summary>
    /// Renders a template identified by its path relative to the template root.
    /// </summary>
    /// <param name="templatePath">The path of the template to render, relative to the initialized template directory (e.g., "default.html" or "includes/header.html").</param>
    /// <param name="dataModel">The primary data object available in the template (often accessible via 'model').</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered HTML string.</returns>
    public Task<string> RenderAsync(string templatePath, object dataModel, CancellationToken cancellationToken = default) {
        // Normalize template path key (use forward slashes)
        string templateKey = templatePath.Replace('\\', '/');
        _logger.LogDebug("Rendering template: {TemplateKey}", templateKey);

        if (_templateFileSystem == null) {
            _logger.LogError("Template engine not initialized. Call InitializeAsync first.");
            throw new InvalidOperationException("Template engine has not been initialized.");
        }

        if (string.IsNullOrWhiteSpace(_includesRootPath)) { // Check if includes path was set
            _logger.LogError("Includes path not set during initialization. Cannot resolve includes correctly.");
            throw new InvalidOperationException("Includes path missing in ScribanTemplateEngine.");
        }

        if (!_cachedTemplates.TryGetValue(templateKey, out var template)) {
            _logger.LogError("Template not found in cache using key: {TemplateKey}. Available keys: [{AvailableKeys}]", templateKey, string.Join(", ", _cachedTemplates.Keys));
            // Consider fallback or throwing a more specific exception
            throw new FileNotFoundException($"Template '{templateKey}' not found in cache.", templateKey);
        }

        try {
            // Create context for this render operation. Contexts are not thread-safe.
            var context = new TemplateContext {
                // Enable auto-indentation for includes if desired
                // AutoIndent = true,
                TemplateLoader = new FileSystemTemplateLoader(_templateFileSystem, _includesRootPath, _logger), // Use custom loader
                MemberRenamer = member => member.Name, // Ensure C# names are used
                // Enable CalculateRegexTimeOut as per Scriban recommendation if using complex regex in templates
                EnableRelaxedMemberAccess = true // Might be useful depending on data models
            };            

            // Create the main model ScriptObject
            var modelScriptObject = new ScriptObject();
            // Import properties and fields. Control member visibility and renaming if needed.
            modelScriptObject.Import(dataModel,
                renamer: member => member.Name,
                filter: member => member switch {
                    PropertyInfo pi => pi.CanRead && pi.GetGetMethod(nonPublic: false) != null,
                    FieldInfo fi => fi.IsPublic,
                    _ => false
                });

            // Make the model available globally in the template (e.g., access properties via 'model.Title')
            // Choose a consistent name like 'model'.
            context.PushGlobal(new ScriptObject { { "model", modelScriptObject } });

            // Optional: Add other global objects like 'site' if the dataModel doesn't already contain everything
            if (dataModel is ContentItem contentItem && contentItem.SiteContext != null) {
                var siteDataScriptObject = new ScriptObject();
                siteDataScriptObject.Import(contentItem.SiteContext,
                renamer: member => member.Name,
                filter: member => member switch {
                    PropertyInfo pi => pi.CanRead && pi.GetGetMethod(nonPublic: false) != null,
                    FieldInfo fi => fi.IsPublic,
                    _ => false
                });
                
                // Maybe add site configuration directly under 'site'?
                var configScriptObject = new ScriptObject();
                configScriptObject.Import(contentItem.SiteContext.Configuration,
                renamer: member => member.Name,
                filter: member => member switch {
                    PropertyInfo pi => pi.CanRead && pi.GetGetMethod(nonPublic: false) != null,
                    FieldInfo fi => fi.IsPublic,
                    _ => false
                });
                // Add config to siteObject to access via site.config.BaseUrl
                // Add 'config' object to the global context
                context.MemberRenamer = member => member.Name; // Ensure renamer is set on context too
                context.PushGlobal(new ScriptObject { { "config", configScriptObject } });

                // Add 'site' object (containing Posts, Pages, Taxonomies lists) to the global context
                context.PushGlobal(new ScriptObject { { "site", siteDataScriptObject } });
            }

            // --- Add Custom Functions ---
            var functions = new ScriptObject();
            // Create a delegate instance for the static Slugify method
            Func<string?, string> slugifyDelegate = StringUtils.Slugify;
            // Add it to the functions object
            functions.Import("slugify", slugifyDelegate);

            // Add other utility functions here if needed
            // functions.SetValue("my_other_func", ...);

            // Push the functions object globally, making 'slugify' accessible
            context.PushGlobal(functions);

            cancellationToken.ThrowIfCancellationRequested();

            // Render the template. Runtime errors during rendering will throw exceptions (e.g., ScriptRuntimeException)
            string result = template.Render(context);

            // REMOVED: Incorrect check for runtime errors.
            // Runtime errors are caught by the catch block below.
            // if (context.HasErrors) { ... }

            _logger.LogTrace("Successfully rendered template: {TemplateKey}", templateKey);
            return Task.FromResult(result);
        }
        catch (ScriptRuntimeException scribanEx) // Catch specific Scriban runtime exceptions
        {
            _logger.LogError(scribanEx, "Scriban runtime error rendering template: {TemplateKey} at {SourceSpan}\n{ScribanMessage}",
                             templateKey, scribanEx.Span, scribanEx.OriginalMessage ?? scribanEx.Message);
            throw; // Re-throw the specific exception or a custom one
        }
        catch (Exception ex) // Catch any other unexpected errors during rendering
        {
            _logger.LogError(ex, "Unexpected error rendering template: {TemplateKey}", templateKey);
            throw; // Re-throw
        }
    }

    // Helper to log template errors
    private void LogTemplateErrors(IEnumerable<LogMessage> messages, string templatePath) {
        _logger.LogError("Errors encountered in template '{TemplatePath}':", templatePath);
        foreach (var message in messages) {
            _logger.LogError("- {ErrorMessage} at {SourceSpan}", message.Message, message.Span);
        }
    }


    // --- Custom Template Loader using IFileSystem ---
    private class FileSystemTemplateLoader : ITemplateLoader {
        private readonly IFileSystem _fileSystem;
        private readonly string _includesRoot; // The base directory for templates (e.g., "layouts") within the IFileSystem context
        private readonly ILogger _logger;

        public FileSystemTemplateLoader(IFileSystem fileSystem, string includesRoot, ILogger logger) {
            _fileSystem = fileSystem;
            _includesRoot = includesRoot;
            _logger = logger;
        }

        // Gets the absolute path within the IFileSystem context
        public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName) {
            // Resolve templateName relative to the templateRoot
            // Normalize separators just in case
            string normalizedTemplateName = templateName.Replace('\\', '/');
            // Combine using the IFileSystem's combine method, ensuring it stays within the site source bounds
            string pathRelativeToSourceRoot = _fileSystem.CombinePath(_includesRoot, normalizedTemplateName);

            _logger.LogTrace("TemplateLoader resolved '{TemplateName}' to absolute path '{AbsolutePath}'", templateName, pathRelativeToSourceRoot);
            return pathRelativeToSourceRoot; // Return path relative to the IFileSystem root
        }

        // Loads the template content
        public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath) {
            // templatePath here is the absolute path returned by GetPath
            _logger.LogTrace("TemplateLoader loading: {TemplatePath}", templatePath);
            try {
                // Use ReadAllTextAsync - block here as Scriban loader is sync, but underlying FS is async capable
                // Consider making the engine fully async if Scriban supports async loaders in future
                // For now, use .GetAwaiter().GetResult() - use with caution, only suitable if called rarely or in non-sync-over-async sensitive contexts
                // TODO: Revisit if this causes deadlocks in certain hosting environments (ASP.NET Core). Alternative: make RenderAsync truly async.
                // Since our builders (CLI, Lambda, Functions) might not have a SynchronizationContext issue, this might be acceptable.
                return _fileSystem.ReadAllTextAsync(templatePath).GetAwaiter().GetResult();
            }
            catch (FileNotFoundException) {
                _logger.LogError("Include template not found by loader: {TemplatePath}", templatePath);
                throw; // Re-throw for Scriban to handle
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error loading include template: {TemplatePath}", templatePath);
                throw; // Re-throw for Scriban to handle
            }
        }

        // Optional: Async version (if Scriban adds support)
        public async ValueTask<string> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath) {
            _logger.LogTrace("TemplateLoader loading async: {TemplatePath}", templatePath);
            try {
                return await _fileSystem.ReadAllTextAsync(templatePath); // Use await directly
            }
            catch (FileNotFoundException) {
                _logger.LogError("Include template not found by loader: {TemplatePath}", templatePath);
                throw;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error loading include template: {TemplatePath}", templatePath);
                throw;
            }
        }
    }
}
