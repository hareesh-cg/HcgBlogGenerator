using HcgBlogGenerator.Core.Models;

namespace HcgBlogGenerator.Core.Abstractions;

/// <summary>
/// Renders templates using provided data models.
/// </summary>
public interface ITemplateEngine {
    /// <summary>
    /// Renders a template file with the given data model.
    /// </summary>
    /// <param name="templatePath">The relative path to the template file (e.g., "layouts/post.html").</param>
    /// <param name="dataModel">The data object to use for rendering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered string output.</returns>
    Task<string> RenderAsync(string templatePath, object dataModel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optional: Pre-loads or registers templates if the engine requires it.
    /// </summary>
    /// <param name="templateDirectory">Directory containing templates and includes.</param>
    /// <param name="fileSystem">Filesystem to read templates from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task indicating completion.</returns>
    Task InitializeAsync(SiteConfiguration configuration, IFileSystem fileSystem, CancellationToken cancellationToken = default);
}
