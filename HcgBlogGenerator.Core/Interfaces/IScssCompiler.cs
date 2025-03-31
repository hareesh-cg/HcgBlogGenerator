 namespace HcgBlogGenerator.Core.Interfaces;

/// <summary>
/// Defines the contract for compiling SCSS/SASS code into CSS.
/// </summary>
public interface IScssCompiler
{
    /// <summary>
    /// Compiles the SCSS content from a given file path into CSS.
    /// </summary>
    /// <param name="inputPath">The path to the SCSS file to compile.</param>
    /// <param name="includePaths">A list of paths to search for @import directives.</param>
    /// <param name="outputStyle">The desired output style for the CSS (e.g., Compressed, Expanded).</param>
    /// <param name="generateSourceMap">Whether to generate a source map file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous compilation operation.
    /// The task result contains a tuple:
    /// - string: The compiled CSS code.
    /// - string?: The source map content, or null if not generated.
    /// Returns (null, null) if compilation fails.
    /// </returns>
    Task<(string? Css, string? SourceMap)> CompileFileAsync(
        string inputPath,
        IEnumerable<string>? includePaths = null,
        ScssOutputStyle outputStyle = ScssOutputStyle.Compressed,
        bool generateSourceMap = false,
        CancellationToken cancellationToken = default);

    // Consider adding CompileStringAsync if needed later:
    // Task<(string? Css, string? SourceMap)> CompileStringAsync(
    //     string scssContent,
    //     IEnumerable<string>? includePaths = null,
    //     ScssOutputStyle outputStyle = ScssOutputStyle.Compressed,
    //     bool generateSourceMap = false,
    //     CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the output style for compiled CSS.
/// Mirrors SharpScss.OutputStyle but defined here to avoid direct dependency in the interface.
/// </summary>
public enum ScssOutputStyle
{
    /// <summary>
    /// Expanded, readable CSS format.
    /// </summary>
    Expanded,
    /// <summary>
    /// Compact, minified CSS format.
    /// </summary>
    Compressed
    // Add other styles like Nested, Compact if needed
}