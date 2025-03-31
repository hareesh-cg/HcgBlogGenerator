using HcgBlogGenerator.Core.Interfaces;
using Microsoft.Extensions.Logging;
using SharpScss;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Compiles SCSS/SASS code into CSS using the SharpScss library (libsass wrapper).
/// </summary>
public class ScssCompiler : IScssCompiler {
    private readonly ILogger<ScssCompiler> _logger;
    private readonly IFileSystem _fileSystem; // Needed to check file existence

    public ScssCompiler(ILogger<ScssCompiler> logger, IFileSystem fileSystem) {
        _logger = logger;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public async Task<(string? Css, string? SourceMap)> CompileFileAsync(
        string inputPath,
        IEnumerable<string>? includePaths = null,
        ScssOutputStyle outputStyle = ScssOutputStyle.Compressed,
        bool generateSourceMap = false,
        CancellationToken cancellationToken = default) {
        if (!await _fileSystem.FileExistsAsync(inputPath)) {
            _logger.LogError("SCSS input file not found: {InputPath}", inputPath);
            return (null, null);
        }

        // Map our enum to SharpScss enum
        var sharpScssOutputStyle = outputStyle switch {
            ScssOutputStyle.Expanded => ScssOutputStyle.Expanded, // Fully qualified
            ScssOutputStyle.Compressed => ScssOutputStyle.Compressed, // Fully qualified
            _ => ScssOutputStyle.Compressed // Default to compressed - Fully qualified
        };

        var options = new ScssOptions {
            InputFile = inputPath, // Let SharpScss handle reading the file directly
            OutputStyle = sharpScssOutputStyle,
            GenerateSourceMap = generateSourceMap,
            // IncludePaths will be populated below
        };

        if (includePaths != null) {
            foreach (var path in includePaths) {
                options.IncludePaths.Add(path);
            }
        }


        // Add the directory of the input file to include paths automatically
        // This is standard behavior for most SCSS compilers for relative imports.
        var inputFileDirectory = _fileSystem.GetDirectoryName(inputPath);
        if (!string.IsNullOrEmpty(inputFileDirectory) && !options.IncludePaths.Contains(inputFileDirectory)) {
            options.IncludePaths.Add(inputFileDirectory);
        }

        _logger.LogDebug("Compiling SCSS file: {InputPath} with OutputStyle={OutputStyle}, SourceMap={GenerateSourceMap}, IncludePaths={IncludePaths}",
            inputPath, options.OutputStyle, options.GenerateSourceMap, string.Join(";", options.IncludePaths));

        try {
            
            // SharpScss compilation is CPU-bound and synchronous.
            // Run it on a background thread to avoid blocking the caller if it's in an async context.
            var result = await Task.Run(() => Scss.ConvertFileToCss(inputPath, options), cancellationToken); // Pass options object

            _logger.LogInformation("Successfully compiled SCSS file: {InputPath}", inputPath);
            return (result.Css, result.SourceMap);
        }
        catch (ScssException scssEx)
        {
            // Check for compilation errors reported by libsass
            _logger.LogError(scssEx, "SCSS compilation failed for {InputPath}.", inputPath);
            return (null, null);
        }
        // Catch potential exceptions from SharpScss itself (e.g., file access issues it might handle internally)
        catch (DllNotFoundException dllEx) // Specifically catch this common SharpScss setup issue
        {
            _logger.LogError(dllEx, "SharpScss native library (libsass) not found or could not be loaded. Ensure the correct SharpScss.runtime package is referenced for your platform. Error compiling {InputPath}", inputPath);
            return (null, null);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Unexpected error during SCSS compilation for {InputPath}", inputPath);
            return (null, null);
        }
    }
}
