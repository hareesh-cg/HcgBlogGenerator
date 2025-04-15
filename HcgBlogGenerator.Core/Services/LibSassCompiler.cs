using HcgBlogGenerator.Core.Abstractions;

using LibSassHost;

using Microsoft.Extensions.Logging;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Implements ICssCompiler using the LibSassHost library.
/// </summary>
public class LibSassCompiler : ICssCompiler {
    private readonly ILogger<LibSassCompiler> _logger;

    // Static initialization for LibSassHost (optional, but can ensure native libs are ready)
    static LibSassCompiler() {
        // You might call SassCompiler.Initialize() here if needed,
        // but it's often handled automatically on first use.
        // Ensure native dependencies are copied to the output directory.
    }

    public LibSassCompiler(ILogger<LibSassCompiler> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogDebug("LibSassCompiler initialized.");
    }

    /// <summary>
    /// Compiles SCSS/SASS content to CSS using LibSassHost.
    /// </summary>
    public Task<string?> CompileAsync(
        string sourceContent,
        string sourcePath,
        IEnumerable<string> includePaths,
        CssOutputStyle outputStyle = CssOutputStyle.Compressed,
        CancellationToken cancellationToken = default) {
        _logger.LogDebug("Compiling CSS for source: {SourcePath}", sourcePath);

        var options = new CompilationOptions {
            // Convert our enum to LibSassHost enum
            OutputStyle = outputStyle == CssOutputStyle.Compressed
                            ? OutputStyle.Compressed
                            : OutputStyle.Expanded,

            // Set include paths for resolving @import directives
            IncludePaths = includePaths?.ToList() ?? new List<string>(),

            // Generate source maps if needed (might be configurable later)
            SourceMap = false, // Keep false for now unless explicitly needed

            // Precision for numeric values (default is usually fine)
            // Precision = 5
        };

        try {
            cancellationToken.ThrowIfCancellationRequested();

            // LibSassHost's Compile method is synchronous.
            // Run it on a background thread if it's potentially long-running
            // or if called from a context sensitive to blocking (less likely here).
            // For simplicity now, call it directly and wrap in Task.FromResult.
            // Revisit if performance profiling shows issues.
            CompilationResult result = SassCompiler.Compile(sourceContent, sourcePath, options: options);

            _logger.LogTrace("Successfully compiled SCSS for {SourcePath}", sourcePath);
            return Task.FromResult<string?>(result.CompiledContent);
        }
        catch (SassCompilationException sassEx) {
            // Catch specific LibSass exceptions
            _logger.LogError(sassEx, "SCSS compilation error for {SourcePath}: {ErrorMessage}\nFile: {File}, Line: {Line}, Column: {Column}",
                             sourcePath, sassEx.Description, sassEx.File, sassEx.LineNumber, sassEx.ColumnNumber);
            return Task.FromResult<string?>(null);
        }
        catch (DllNotFoundException dllEx) {
            _logger.LogCritical(dllEx, "LibSass native library not found. Ensure the correct LibSassHost.Native.* package is referenced and deployed for the current platform/architecture.");
            throw; // Re-throw this critical error
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Unexpected error during SCSS compilation for {SourcePath}", sourcePath);
            return Task.FromResult<string?>(null); // Indicate failure on general errors
            // Or re-throw if preferred: throw;
        }
    }
}
