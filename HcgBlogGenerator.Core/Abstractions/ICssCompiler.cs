namespace HcgBlogGenerator.Core.Abstractions;

/// <summary>
/// Compiles CSS preprocessor languages (like SCSS/SASS) into standard CSS.
/// </summary>
public interface ICssCompiler {
    /// <summary>
    /// Compiles a CSS preprocessor file content into standard CSS.
    /// </summary>
    /// <param name="sourceContent">The string content of the source file (e.g., SCSS).</param>
    /// <param name="sourcePath">The original path of the source file (used for resolving imports and error reporting).</param>
    /// <param name="includePaths">Additional paths to search for @import directives.</param>
    /// <param name="outputStyle">Desired output style (e.g., Compressed, Expanded).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task resulting in the compiled CSS string, or null/throws on error.</returns>
    Task<string?> CompileAsync(
        string sourceContent,
        string sourcePath,
        IEnumerable<string> includePaths,
        CssOutputStyle outputStyle = CssOutputStyle.Compressed,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the output style for compiled CSS.
/// </summary>
public enum CssOutputStyle {
    Expanded,
    Compressed
}
