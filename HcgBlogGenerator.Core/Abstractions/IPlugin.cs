using HcgBlogGenerator.Core.Models;

namespace HcgBlogGenerator.Core.Abstractions;

/// <summary>
/// Interface for HcgBlogGenerator plugins.
/// Plugins allow extending the build process at various stages.
/// </summary>
public interface IPlugin {
    /// <summary>
    /// Gets the unique name or identifier of the plugin.
    /// Used for logging and potentially configuration.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the plugin's logic for the specified pipeline stage.
    /// </summary>
    /// <param name="stage">The current pipeline stage being executed.</param>
    /// <param name="siteContext">The global site context containing configuration and processed data.</param>
    /// <param name="sourceFileSystem">Filesystem for reading source files.</param>
    /// <param name="outputFileSystem">Filesystem for writing output files (especially relevant for PostBuild stage).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Plugins should check the 'stage' parameter and only execute logic relevant to that stage.
    /// Implementations should be idempotent where possible for a given stage.
    /// The exact data available in 'siteContext' depends on the 'stage'.
    /// </remarks>
    Task ExecuteAsync(
        PipelineStage stage,
        SiteContext siteContext,
        IFileSystem sourceFileSystem, // Added source FS for more flexibility
        IFileSystem outputFileSystem,
        CancellationToken cancellationToken = default);
}
