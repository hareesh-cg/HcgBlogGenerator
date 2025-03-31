 using HcgBlogGenerator.Core.Interfaces;
using Microsoft.Extensions.Logging;
using SharpScss; // Requires the SharpScss NuGet package

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Compiles SCSS/SASS code into CSS using the SharpScss library (libsass wrapper).
/// </summary>
public class ScssCompiler : IScssCompiler
{
    private readonly ILogger<ScssCompiler> _logger;
    private readonly IFileSystem _fileSystem; // Needed to check file existence

    public ScssCompiler(ILogger<ScssCompiler> logger, IFileSystem fileSystem)
    {
        _logger = logger;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public async Task<(string? Css, string? SourceMap)> CompileFileAsync(
        string inputPath,
        IEnumerable<string>? includePaths = null,
        ScssOutputStyle outputStyle = ScssOutputStyle.Compressed,
        bool generateSourceMap = false,
        CancellationToken cancellationToken = default)
    {
        if (!await _fileSystem.FileExistsAsync(inputPath))
        {
            _logger.LogError("SCSS input file not found: {InputPath}", inputPath);
            return (null, null);
        }

        // Map our enum to SharpScss enum
        var sharpScssOutputStyle = outputStyle switch
        {
            ScssOutputStyle.Expanded => OutputStyle.Expanded,
            ScssOutputStyle.Compressed => OutputStyle.Compressed,
            _ => OutputStyle.Compressed // Default to compressed
        };

        var options = new ScssOptions
        {
            InputFile = inputPath, // Let SharpScss handle reading the file directly
            OutputStyle = sharpScssOutputStyle,
            GenerateSourceMap = generateSourceMap,
            IncludePaths = includePaths?.ToList() ?? new List<string>()
        };

        // Add the directory of the input file to include paths automatically
        // This is standard behavior for most SCSS compilers for relative imports.
        var inputFileDirectory = _fileSystem.GetDirectoryName(inputPath);
        if (!string.IsNullOrEmpty(inputFileDirectory) && !options.IncludePaths.Contains(inputFileDirectory))
        {
            options.IncludePaths.Add(inputFileDirectory);
        }

        _logger.LogDebug("Compiling SCSS file: {InputPath} with OutputStyle={OutputStyle}, SourceMap={GenerateSourceMap}",
            inputPath, options.OutputStyle, options.GenerateSourceMap);

        try
        {
            // SharpScss compilation is CPU-bound and synchronous.
            // Run it on a background thread to avoid blocking the caller if it's in an async context.
            var result = await Task.Run(() => Scss.CompileFile(inputPath, options), cancellationToken);

            // Check for compilation errors reported by libsass
            if (result.ErrorStatus != 0 || !string.IsNullOrEmpty(result.ErrorMessage))
            {
                _logger.LogError("SCSS compilation failed for {InputPath}. Status: {ErrorStatus}. Error: {ErrorMessage}",
                    inputPath, result.ErrorStatus, result.ErrorMessage?.Trim());
                return (null, null);
            }

            _logger.LogInformation("Successfully compiled SCSS file: {InputPath}", inputPath);
            return (result.Css, result.SourceMap);
        }
        // Catch potential exceptions from SharpScss itself (e.g., file access issues it might handle internally)
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during SCSS compilation for {InputPath}", inputPath);
            return (null, null);
        }
    }
}