using ColorCode.Compilation.Languages;

using HcgBlogGenerator.Core.Abstractions;

using Microsoft.Extensions.Logging;

using SharpScss; // Main namespace

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Implements ICssCompiler using the SharpScss library (libsass wrapper).
/// Uses ScssSettings.IncludePaths and potentially custom importers if needed.
/// </summary>
public class SharpScssCompiler : ICssCompiler {
    private readonly ILogger<SharpScssCompiler> _logger;
    // Cache resolved import content to avoid repeated S3 reads
    private readonly ConcurrentDictionary<string, string> _importContentCache = new();
    IFileSystem _sourceFileSystem;

    public SharpScssCompiler(ILogger<SharpScssCompiler> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // Pre-load native libraries? SharpScss might handle this automatically.
        try {
            var version = Scss.Version;
            _logger.LogDebug("SharpScss (libsass) Version: {Version}", version);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to get SharpScss version. Native libsass might be missing or unloadable.");
        }
    }

    public Task<string?> CompileAsync(
        string sourceContent,
        string sourcePath, // Path relative to sourceFileSystem root
        IFileSystem sourceFileSystem,
        CssOutputStyle outputStyle = CssOutputStyle.Compressed,
        CancellationToken cancellationToken = default) {
        _logger.LogDebug("Compiling CSS for source: {SourcePath} using SharpScss", sourcePath);

        _sourceFileSystem = sourceFileSystem;

        // --- Configure SharpScss Options ---
        var options = new ScssOptions {
            // Map output style
            OutputStyle = outputStyle == CssOutputStyle.Compressed
                            ? ScssOutputStyle.Compressed
                            : ScssOutputStyle.Expanded,

            TryImport = ImportFile

            // GenerateSourceMap = false, // Control source map generation
            // Precision = 5 // Default is usually fine
        };

        // Add the directory of the source file as an include path
        string? sourceDirectory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(sourceDirectory)) {
            options.IncludePaths.Add(sourceDirectory);
            _logger.LogDebug("Adding source directory to include paths: {SourceDir}", sourceDirectory);
        }
        else {
            // If sourcePath has no directory (e.g., "main.scss"), the include path might need to be "." or empty
            // Let's add "." conceptually representing the root relative path start? Or leave empty? Start empty.
            _logger.LogDebug("Source path has no directory, using empty include path list initially.");
        }

        try {
            cancellationToken.ThrowIfCancellationRequested();

            // --- Compile using Scss.ConvertToCss ---
            // This method takes the SCSS content string directly.
            // It uses the IncludePaths (and potentially Importer) from options to resolve @import.
            ScssResult result = Scss.ConvertToCss(sourceContent, options);

            // Check for warnings (SharpScss often logs warnings separately)
            // Currently, no direct warning property on ScssResult, check logs or events if needed.

            _logger.LogInformation("SharpScss compilation successful for source: {SourcePath}", sourcePath);
            return Task.FromResult<string?>(result.Css); // Return compiled CSS
        }
        catch (ScssException scssEx) // Catch SharpScss specific exceptions
        {
            // Log detailed error from SharpScss
            _logger.LogError(scssEx, "SharpScss compilation error for {SourcePath}: {ErrorMessage}\nFile: {File}, Line: {Line}, Column: {Column}",
                             sourcePath, scssEx.Message, scssEx.File ?? sourcePath, scssEx.Line, scssEx.Column);
            return Task.FromResult<string?>(null); // Indicate failure
        }
        catch (DllNotFoundException dllEx) // Still possible with native deps
        {
            _logger.LogCritical(dllEx, "libsass native library not found or loadable for SharpScss. Ensure Scss.Native.* or LibSassBuilder package is referenced correctly in the executable project.");
            throw; // Re-throw critical error
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Unexpected error during SharpScss compilation for {SourcePath}", sourcePath);
            return Task.FromResult<string?>(null); // Indicate failure
        }
    }

    private bool ImportFile(ref string file, string parentPath, out string scss, out string? map) {
        parentPath = "styles/";
        _logger.LogTrace("SharpScss TryImport: File='{File}', ParentPath='{ParentPath}'", file, parentPath);
        scss = string.Empty; // Output parameter for SCSS content
        map = null; // Output parameter for source map (not used here)

        // 'file' is the path from the @import (e.g., 'variables', 'base/base')
        // 'parentPath' is the path of the file doing the import (relative to source root, e.g., styles/main.scss)

        // Key for caching based on requested import relative to parent
        string cacheKey = $"{parentPath}::{file}";
        if (_importContentCache.TryGetValue(cacheKey, out string? cachedScss)) {
            _logger.LogTrace("SharpScss import cache hit for '{CacheKey}'", cacheKey);
            scss = cachedScss!; // Use cached content
                                // Important: Update 'file' ref parameter to signal success, using original file name?
                                // Or maybe an absolute path if resolved? Docs are unclear, let's try setting it
                                // file = resolvedPath; // We need the resolved path here
            return true; // Indicate success
        }


        string? currentDirectory = Path.GetDirectoryName(parentPath)?.Replace('\\', '/');        
        if (string.IsNullOrEmpty(currentDirectory))
            currentDirectory = string.Empty;

        string importPathGuess = _sourceFileSystem.CombinePath(currentDirectory, file);

        // Try potential filenames (relative to source root)
        string[] potentialFiles = {
                     $"{importPathGuess}.scss",
                     $"{_sourceFileSystem.CombinePath(Path.GetDirectoryName(importPathGuess) ?? "", "_" + Path.GetFileName(importPathGuess))}.scss",
                     // Add .sass variations if needed
                 };

        string? resolvedPath = null;

        _logger.LogTrace("Attempting to resolve import '{ImportUrl}' from '{Parent}'.", file, parentPath);
        foreach (var potentialPath in potentialFiles.Distinct()) {
            if (string.IsNullOrWhiteSpace(potentialPath))
                continue;
            _logger.LogTrace("- Checking potential source path: {PotentialPath}", potentialPath);
            try {
                // Use synchronous GetAwaiter().GetResult() as delegate is sync
                bool exists = _sourceFileSystem.FileExistsAsync(potentialPath).GetAwaiter().GetResult();
                if (exists) {
                    resolvedPath = potentialPath;
                    _logger.LogDebug("Resolved import '{ImportUrl}' to source path: {ResolvedPath}", file, resolvedPath);
                    break;
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Error checking potential import path {PotentialPath}", potentialPath); }
        }


        if (resolvedPath != null) {
            try {
                // Read content synchronously
                scss = _sourceFileSystem.ReadAllTextAsync(resolvedPath).GetAwaiter().GetResult();
                _importContentCache.TryAdd(cacheKey, scss); // Add to cache
                _logger.LogTrace("Successfully read content for import '{File}' from '{ResolvedPath}'. Length: {Length}", file, resolvedPath, scss.Length);

                // --- Update ref file parameter ---
                // Option 1: Set to resolved path relative to source root?
                // file = resolvedPath;
                // Option 2: Set to an identifier SharpScss understands?
                // Set to the original requested path 'file' might be safest if unsure.
                // Let's assume SharpScss uses this primarily to know *if* we handled it.
                // Keep 'file' as is, maybe? Or set to resolved? Let's try resolved path.
                file = resolvedPath; // Try setting ref parameter to resolved path

                return true; // Indicate we handled the import
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Failed to read content for resolved import '{ResolvedPath}' for '{File}' from '{Parent}'.", resolvedPath, file, parentPath);
                scss = $"/* Error reading import: {ex.Message} */"; // Provide error content
                return true; // Indicate handled, but with error content
            }
        }
        else {
            _logger.LogWarning("Could not resolve import '{File}' from '{Parent}'. Let LibSass handle it or fail.", file, parentPath);
            return false; // Indicate we did NOT handle the import, let libsass try default resolution (which will fail for S3)
        }
    }
}
