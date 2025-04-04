using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Models; // If needed for specific context data
using HcgBlogGenerator.Core.Utilities;

using Microsoft.Extensions.Logging;

using Scriban;
using Scriban.Parsing; // Required for IScriptObject, ITemplateLoader
using Scriban.Runtime; // Required for ScriptObject, TemplateLoader
using Scriban.Syntax;

using System;
using System.Collections.Concurrent; // For thread-safe dictionary
using System.Collections.Generic;
using System.IO; // For Path operations
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

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
        _templateRootPath = configuration.TemplateDirectory;
        _includesRootPath = configuration.IncludesDirectory;

        _logger.LogInformation("Initializing Scriban template engine. Loading templates from: {TemplateDirectory}", _templateRootPath);
        _cachedTemplates.Clear(); // Clear cache on re-initialization

        // Store the filesystem and root path for the custom loader
        // We assume the passed fileSystem operates relative to the site root,
        // so templateDirectory is a sub-path within that.
        _templateFileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));        

        try {
            // Find all potential template files (e.g., .html, .scriban) recursively
            // Adjust search patterns as needed
            var templateFiles = (await _templateFileSystem.GetFilesAsync(_templateRootPath, "*.*", true, cancellationToken)); //.ToList();            

            int loadedCount = 0;
            int errorCount = 0;

            foreach (var relativePath in templateFiles.Where(f => f.EndsWith(".html", StringComparison.OrdinalIgnoreCase))) {
                cancellationToken.ThrowIfCancellationRequested();
                // The key should be relative to the templateDirectory itself, not the site root
                string templateKey = Path.GetRelativePath(_templateRootPath, relativePath).Replace(Path.DirectorySeparatorChar, '/'); ;

                try {
                    _logger.LogDebug("Loading template: {TemplatePath}", relativePath);
                    string templateContent = await _templateFileSystem.ReadAllTextAsync(relativePath, cancellationToken);

                    // Parse the template. Parsing catches syntax errors early.
                    var template = Template.Parse(templateContent, relativePath); // Use path for better error messages

                    if (template.HasErrors) {
                        errorCount++;
                        LogTemplateErrors(template.Messages, relativePath);
                        // Optionally skip caching errored templates, or cache them to prevent re-parsing
                        // Let's skip caching bad ones for now.
                        continue;
                    }

                    if (!_cachedTemplates.TryAdd(templateKey, template)) {
                        _logger.LogWarning("Could not add template {TemplateKey} to cache (already exists?). Path: {TemplatePath}", templateKey, relativePath);
                    }
                    else {
                        loadedCount++;
                        _logger.LogTrace("Successfully parsed and cached template: {TemplateKey}", templateKey);
                    }
                }
                catch (Exception ex) {
                    errorCount++;
                    _logger.LogError(ex, "Failed to load or parse template file: {TemplatePath}", relativePath);
                }
            }

            _logger.LogInformation("Template initialization complete. Loaded {LoadedCount} templates, encountered {ErrorCount} errors.", loadedCount, errorCount);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed during template initialization process for directory: {TemplateDirectory}", _templateRootPath);
            // Re-throw or handle as appropriate for application lifecycle
            throw;
        }
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
            _logger.LogError("Template not found in cache: {TemplateKey}. Ensure it was loaded during initialization.", templateKey);
            // Consider fallback or throwing a more specific exception
            throw new FileNotFoundException($"Template '{templateKey}' not found or failed to load.", templateKey);
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
