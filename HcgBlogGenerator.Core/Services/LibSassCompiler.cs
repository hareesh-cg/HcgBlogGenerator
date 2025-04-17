using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

using HcgBlogGenerator.Core.Abstractions;

using LibSassHost;

using Microsoft.Extensions.Logging;


namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Implements ICssCompiler using the LibSassHost library.
/// </summary>
public class LibSassCompiler : ICssCompiler {
    private readonly ILogger<LibSassCompiler> _logger;
    // Cache for imported file contents to avoid redundant S3 reads within a single compile
    private readonly ConcurrentDictionary<string, string> _importCache = new();
    private static readonly Regex ImportRegex = new Regex(@"@import\s+['""]([^'""]+)['""];?", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    public LibSassCompiler(ILogger<LibSassCompiler> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogDebug("LibSassCompiler initialized.");
    }

    /// <summary>
    /// Compiles SCSS/SASS content to CSS using LibSassHost.
    /// </summary>
    public async Task<string?> CompileAsync(
        string sourceContent,
        string sourcePath,
        IFileSystem sourceFileSystem,
        CssOutputStyle outputStyle = CssOutputStyle.Compressed,
        CancellationToken cancellationToken = default) {

        _logger.LogDebug("Attempting manual SCSS import resolution for {SourcePath}", sourcePath);

        try {
            // 1. Resolve all imports recursively and build combined content
            var resolvedContent = new StringBuilder();
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Track processed files to prevent infinite loops

            await ResolveAndAppendImportsAsync(sourcePath, sourceFileSystem, resolvedContent, processedFiles, cancellationToken);

            string finalScss = resolvedContent.ToString();
            _logger.LogDebug("Manual import resolution complete. Total SCSS length: {Length}", finalScss.Length);
            // For debugging: Log the combined SCSS (can be very long)
            // _logger.LogTrace("Combined SCSS:\n{SCSS}", finalScss);


            // 2. Compile the combined string (which should have no @import)
            var options = new CompilationOptions {
                OutputStyle = outputStyle == CssOutputStyle.Compressed
                                ? OutputStyle.Compressed
                                : OutputStyle.Expanded,
                SourceMap = false
                // No importer needed now
            };

            // Compile the combined content. Pass the original sourcePath for context if needed by compiler internals,
            // though source map generation won't work correctly.
            CompilationResult result = SassCompiler.Compile(finalScss, sourcePath, options: options); // Pass original path for context

            _logger.LogInformation("Successfully compiled combined SCSS from {SourcePath}", sourcePath);
            return result.CompiledContent;
        }
        catch (SassCompilationException sassEx) {
            // Catch specific LibSass exceptions
            _logger.LogError(sassEx, "SCSS compilation error for {SourcePath}: {ErrorMessage}\nFile: {File}, Line: {Line}, Column: {Column}",
                             sourcePath, sassEx.Description, sassEx.File, sassEx.LineNumber, sassEx.ColumnNumber);
            return null;
        }
        catch (DllNotFoundException dllEx) {
            _logger.LogCritical(dllEx, "LibSass native library not found. Ensure the correct LibSassHost.Native.* package is referenced and deployed for the current platform/architecture.");
            throw; // Re-throw this critical error
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Unexpected error during SCSS compilation for {SourcePath}", sourcePath);
            return null; // Indicate failure on general errors
            // Or re-throw if preferred: throw;
        }
    }

    // --- Recursive Helper Method --- (Needs significant error handling and path logic refinement)
    private async Task ResolveAndAppendImportsAsync(
        string filePath,
        IFileSystem fileSystem,
        StringBuilder outputContent,
        HashSet<string> processedFiles,
        CancellationToken cancellationToken) {

        string processingKey = filePath.Replace('\\', '/').TrimStart('/');
        // Prevent circular dependencies / redundant processing
        if (!processedFiles.Add(processingKey)) {
            _logger.LogTrace("Already processed {FilePath}, skipping.", processingKey);
            return;
        }

        _logger.LogDebug("Processing file for manual import substitution: {FilePath}", processingKey);

        string content;
        try {
            content = await fileSystem.ReadAllTextAsync(processingKey, cancellationToken);
        }
        catch (FileNotFoundException) {
            _logger.LogError("Could not read file content for {FilePath} during import resolution.", processingKey);
            throw; // Fail fast
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error reading file content for {FilePath} during import resolution.", processingKey);
            throw; // Fail fast
        }

        var matches = ImportRegex.Matches(content);
        int lastIndex = 0;

        if (!matches.Any()) {
            // If no imports, just append the whole file content
            _logger.LogTrace("No imports found in {FilePath}, appending content directly.", processingKey);
            outputContent.Append(content); // Append the entire content if no imports
            outputContent.AppendLine(); // Ensure newline
        }
        else {
            _logger.LogTrace("Found {Count} import(s) in {FilePath}", matches.Count, processingKey);
            foreach (Match match in matches.Cast<Match>()) {
                // Append content *before* the current @import line
                outputContent.Append(content, lastIndex, match.Index - lastIndex);
                _logger.LogTrace("Appended content chunk before import '{ImportUrl}'", match.Groups[1].Value);


                // --- Resolve and Process the Import ---
                string importUrl = match.Groups[1].Value; // e.g., "variables", "base/base"
                string? resolvedImportPath = await ResolveImportPathAsync(processingKey, importUrl, fileSystem, cancellationToken);

                if (resolvedImportPath != null) {
                    _logger.LogDebug("Recursively processing resolved import: {ResolvedPath}", resolvedImportPath);
                    // Recursively process the imported file, appending its processed content
                    await ResolveAndAppendImportsAsync(resolvedImportPath, fileSystem, outputContent, processedFiles, cancellationToken);
                    // DO NOT append the original @import line itself
                }
                else {
                    _logger.LogError("Could not resolve @import '{ImportUrl}' found in {FilePath}. This @import line will be OMITTED.", importUrl, processingKey);
                    // Optionally throw an error here instead of omitting
                    // throw new FileNotFoundException($"Could not resolve SASS import '{importUrl}' from '{filePath}'.");
                }
                // ---

                // Advance lastIndex PAST the processed @import line
                lastIndex = match.Index + match.Length;
            }

            // Append remaining content *after* the last @import
            if (lastIndex < content.Length) {
                outputContent.Append(content, lastIndex, content.Length - lastIndex);
                _logger.LogTrace("Appended final content chunk of {FilePath}", processingKey);
            }
            outputContent.AppendLine(); // Ensure newline after file processing
        }
    }

    // --- NEW Separate Helper for Resolving Import Path ---
    private async Task<string?> ResolveImportPathAsync(
         string parentFilePathRel, // File containing the @import (relative to source root)
         string importUrl,         // Path from the @import directive
         IFileSystem fileSystem,
         CancellationToken cancellationToken) {
        string? currentDirectory = Path.GetDirectoryName(parentFilePathRel)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(currentDirectory))
            currentDirectory = string.Empty;

        // Base path relative to current dir
        string importPathGuess = fileSystem.CombinePath(currentDirectory, importUrl);

        // Directory part of the import guess
        string directoryOfGuess = Path.GetDirectoryName(importPathGuess)?.Replace('\\', '/') ?? "";
        // Filename part of the import guess (without extension initially)
        string fileNamePart = Path.GetFileName(importPathGuess);

        // Build potential paths relative to source root
        string[] potentialFilesRelToSource = {
            $"{fileSystem.CombinePath(directoryOfGuess, "_" + fileNamePart)}.scss",
            $"{importPathGuess}.scss",
        // Add .sass variations if needed
        // $"{sourceFileSystem.CombinePath(directoryOfGuess, "_" + fileNamePart)}.sass",
        // $"{importPathGuess}.sass",
        };

        _logger.LogTrace("Attempting to resolve import '{ImportUrl}' relative to '{ParentDir}'. Base guess: '{BaseGuess}'", importUrl, currentDirectory, importPathGuess);
        foreach (var potentialPath in potentialFilesRelToSource.Distinct()) {
            if (string.IsNullOrWhiteSpace(potentialPath))
                continue;
            _logger.LogTrace("- Checking potential source path: {PotentialPath}", potentialPath);
            try {
                if (await fileSystem.FileExistsAsync(potentialPath, cancellationToken)) {
                    _logger.LogDebug("Resolved import '{ImportUrl}' to source file: {ResolvedPath}", importUrl, potentialPath);
                    return potentialPath; // Return the resolved path relative to source root
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Error checking potential import path {PotentialPath}", potentialPath); }
        }

        // If not found, return null
        return null;
    }
}
