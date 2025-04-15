namespace HcgBlogGenerator.Core.Abstractions;

/// <summary>
/// Orchestrates the entire static site generation process.
/// </summary>
public interface ISiteBuilder {
    /// <summary>
    /// Builds the static site based on the provided configuration.
    /// </summary>
    /// <param name="configPath">Path to the site configuration file (e.g., config.json).</param>
    /// <param name="sourceFileSystem">Filesystem representing the source directory.</param>
    /// <param name="outputFileSystem">Filesystem representing the output directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task indicating completion of the build process.</returns>
    Task BuildAsync(string configPath, IFileSystem sourceFileSystem, IFileSystem outputFileSystem, CancellationToken cancellationToken = default);
}
