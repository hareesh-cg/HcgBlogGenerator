using System.Text.RegularExpressions;

using DartSassHost; // Main namespace

using HcgBlogGenerator.Core.Abstractions;

using Microsoft.Extensions.Logging;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Implements ICssCompiler using DartSassHost, compiling files on a temporary local filesystem.
/// </summary>
public class DartSassCompiler : ICssCompiler {
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DartSassCompiler> _logger;
    // DartSassHost compiler can often be reused
    private static readonly Lazy<SassCompiler> _compiler = new Lazy<SassCompiler>(() => new SassCompiler());
    private SassCompiler Compiler => _compiler.Value;

    // Regex to find simple @import "..." or @import '...'
    private static readonly Regex ImportRegex = new Regex(@"^\s*@import\s+['""]([^'""]+)['""];?", RegexOptions.Multiline | RegexOptions.Compiled);


    public DartSassCompiler(ILoggerFactory loggerFactory) {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        // Create logger for this specific class
        _logger = _loggerFactory.CreateLogger<DartSassCompiler>();
        _logger.LogDebug("DartSassCompiler initialized.");
        // Test if compiler loads on init (catches deployment issues early)
        try {
            var version = Compiler.Version;
            _logger.LogDebug("Dart Sass Version: {Version}", version);
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to initialize Dart Sass compiler. Ensure runtime is deployed correctly."); }
    }

    // CompileAsync now orchestrates download, local compile, and cleanup
    public async Task<string?> CompileAsync(
        string sourceContent, // No longer directly used for compilation, but useful for initial write
        string sourcePath,    // Path relative to sourceFileSystem (e.g., styles/main.scss)
        IFileSystem sourceFileSystem, // The source (e.g., S3)
        CssOutputStyle outputStyle = CssOutputStyle.Compressed,
        CancellationToken cancellationToken = default) {
        // 1. Create unique temporary directory in Lambda's /tmp or OS temp
        // Must dispose this directory properly
        string tempDir = Path.Combine(Path.GetTempPath(), $"hcg-sass-{Path.GetRandomFileName()}");
        string tempSourceFilePath = Path.Combine(tempDir, sourcePath.Replace('/', Path.DirectorySeparatorChar)); // Local OS path for main file

        try {
            _logger.LogInformation("Preparing temporary directory for SCSS compilation: {TempDir}", tempDir);
            Directory.CreateDirectory(Path.GetDirectoryName(tempSourceFilePath) ?? tempDir); // Ensure directory exists

            // Create a LocalFileSystem for the temp directory for internal operations
            var tempFs = new LocalFileSystem(tempDir, false, _loggerFactory.CreateLogger<LocalFileSystem>()); // Assuming logger compatible or create specific one

            // 2. Download main file and resolve/download imports recursively
            var processedImports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await DownloadImportsRecursiveAsync(sourcePath, sourceFileSystem, tempFs, processedImports, cancellationToken);

            // Sanity check: Ensure main file was downloaded (should be covered by recursive call)
            if (!File.Exists(tempSourceFilePath)) {
                // Write initial content if recursive download missed it somehow (shouldn't happen)
                _logger.LogWarning("Main source file {File} not found in temp dir after import download, writing initial content.", tempSourceFilePath);
                await tempFs.WriteAllTextAsync(sourcePath, sourceContent, cancellationToken); // Write using relative path for tempFs
            }

            // 3. Compile the file *in the temporary directory* using DartSassHost
            _logger.LogInformation("Compiling local SCSS file: {TempSourcePath}", tempSourceFilePath);
            var options = new CompilationOptions {
                OutputStyle = outputStyle == CssOutputStyle.Compressed
                    ? DartSassHost.OutputStyle.Compressed
                    : DartSassHost.OutputStyle.Expanded,
                SourceMap = false // Keep source maps off for now
                // IncludePaths not strictly needed if relative paths work in temp dir
            };

            // CompileFile works on the local filesystem path
            CompilationResult result = Compiler.CompileFile(tempSourceFilePath, options: options);

            _logger.LogInformation("SCSS compilation successful for source: {SourcePath}", sourcePath);
            return result.CompiledContent;

        }
        // Catch DartSassCompilationException specifically
        catch (SassCompilationException sassEx) {
            _logger.LogError(sassEx, "Dart Sass compilation error for {SourcePath}: {ErrorMessage}\nFile: {File}, Line: {Line}, Column: {Column}\nSource Fragment: {SourceFragment}",
                             sourcePath, sassEx.Description, sassEx.File, sassEx.LineNumber, sassEx.ColumnNumber, sassEx.SourceFragment);
            return null; // Indicate failure
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Unexpected error during SCSS compilation preparation or execution for {SourcePath}", sourcePath);
            return null; // Indicate failure
        }
        finally {
            // 4. Clean up temporary directory
            try {
                if (Directory.Exists(tempDir)) {
                    _logger.LogDebug("Cleaning up temporary directory: {TempDir}", tempDir);
                    Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to clean up temporary SCSS directory: {TempDir}", tempDir);
            }
        }
    }

    // Recursive helper to download imports
    private async Task DownloadImportsRecursiveAsync(
        string filePathRelToSource, // e.g., styles/main.scss or styles/_variables.scss
        IFileSystem sourceFs,       // Where to read from (e.g., S3)
        LocalFileSystem tempFs,     // Where to write to (local /tmp/)
        HashSet<string> processedFiles,
        CancellationToken cancellationToken) {
        // Use normalized path relative to source root as the key
        string processingKey = filePathRelToSource.Replace('\\', '/').TrimStart('/');

        if (!processedFiles.Add(processingKey)) {
            _logger.LogTrace("Already processed/downloaded {File}, skipping.", processingKey);
            return; // Already processed or in progress
        }

        _logger.LogDebug("Downloading/Processing SCSS file for local compilation: {File}", processingKey);

        string fileContent;
        try {
            fileContent = await sourceFs.ReadAllTextAsync(processingKey, cancellationToken);
            // Write file to temp directory using path relative to temp root (same as key)
            await tempFs.WriteAllTextAsync(processingKey, fileContent, cancellationToken);
            _logger.LogTrace("Successfully downloaded and wrote {File} to temp directory.", processingKey);
        }
        catch (FileNotFoundException) {
            _logger.LogError("SCSS source file not found in source filesystem: {File}", processingKey);
            // Re-throw or handle? Re-throwing fails build fast.
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to read/write SCSS file {File} during temp preparation.", processingKey);
            throw; // Fail build if critical files cannot be prepared
        }

        // Find imports within this file's content
        var matches = ImportRegex.Matches(fileContent);
        if (!matches.Any())
            return; // No imports in this file

        _logger.LogTrace("Found {Count} @import statements in {File}", matches.Count, processingKey);

        string? currentDirectory = Path.GetDirectoryName(processingKey)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(currentDirectory))
            currentDirectory = string.Empty;

        foreach (Match match in matches.Cast<Match>()) {
            cancellationToken.ThrowIfCancellationRequested();
            string importUrl = match.Groups[1].Value; // e.g., "variables", "base/base"
            _logger.LogTrace("Found @import '{ImportUrl}' in {File}", importUrl, processingKey);

            // --- Resolve the imported file path RELATIVE TO SOURCE ROOT ---
            string? currentDirectoryRelToSource = Path.GetDirectoryName(processingKey)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(currentDirectoryRelToSource))
                currentDirectoryRelToSource = string.Empty;

            // Combine the directory of the CURRENT file (relative to source root)
            // with the path specified in the @import directive.
            string importPathGuess = sourceFs.CombinePath(currentDirectoryRelToSource, importUrl);
            _logger.LogTrace("Resolving import '{ImportUrl}' relative to '{CurrentDir}'. Potential base: '{ImportPathGuess}'", importUrl, currentDirectoryRelToSource, importPathGuess);

            // Try potential filenames Sass uses (relative to SOURCE ROOT)
            string directoryOfGuess = Path.GetDirectoryName(importPathGuess)?.Replace('\\', '/') ?? "";
            string fileNamePart = Path.GetFileName(importPathGuess); // Can be empty if importUrl ends with /

            string[] potentialFilesRelToSource = {
                    // 1. Direct guess with .scss
                    $"{importPathGuess}.scss",
                    // 2. Direct guess with _ prefix and .scss
                    $"{sourceFs.CombinePath(directoryOfGuess, "_" + fileNamePart)}.scss",
                    // Add .sass variations here if needed
                };

            string? resolvedImportPathRelToSource = null;

            foreach (var potentialPath in potentialFilesRelToSource.Distinct()) {
                if (string.IsNullOrWhiteSpace(potentialPath))
                    continue;
                _logger.LogTrace("- Checking potential source path: {PotentialPath}", potentialPath);
                // Use FileExistsAsync on the source filesystem
                if (await sourceFs.FileExistsAsync(potentialPath, cancellationToken)) {
                    resolvedImportPathRelToSource = potentialPath;
                    _logger.LogDebug("Resolved import '{ImportUrl}' to source path: {ResolvedPath}", importUrl, resolvedImportPathRelToSource);
                    break; // Found it
                }
            }

            if (resolvedImportPathRelToSource != null) {
                // Recursively download the correctly resolved imported file
                await DownloadImportsRecursiveAsync(resolvedImportPathRelToSource, sourceFs, tempFs, processedFiles, cancellationToken);
            }
            else {
                _logger.LogWarning("Could not resolve @import '{ImportUrl}' found in {File}. Checked variations based on '{ImportPathGuess}'. Compilation might fail.", importUrl, processingKey, importPathGuess);
                // Consider throwing here if imports are critical
                // throw new FileNotFoundException($"Could not resolve SASS import '{importUrl}' from '{processingKey}'.");
            }
        }
    }

}
