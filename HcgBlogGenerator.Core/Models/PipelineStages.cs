namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Defines the different stages in the site build pipeline where plugins can be executed.
/// </summary>
public enum PipelineStage {
    /// <summary>
    /// Executed at the very beginning of the build process, after configuration is loaded but before any processing.
    /// Useful for setup tasks or modifying configuration.
    /// </summary>
    PreBuild,

    /// <summary>
    /// Executed after initial content discovery and parsing, but before sorting, taxonomy generation, or rendering.
    /// Useful for modifying raw content items or adding metadata (e.g., reading time).
    /// </summary>
    PostContentProcessing,

    /// <summary>
    /// Executed after all content items (posts, pages, etc.) have been rendered to their final HTML strings,
    /// but before they are written to the output filesystem.
    /// Useful for post-render HTML manipulation (e.g., image optimization references, link adjustments).
    /// Note: This stage might require passing the rendered content string along with the ContentItem.
    /// </summary>
    PostRender, // Might need refinement on how rendered content is passed

    /// <summary>
    /// Executed after all content, CSS, and static files have been written to the output filesystem.
    /// Useful for generating artifacts based on the final site structure (e.g., Sitemap, RSS feed, Robots.txt, Search Index).
    /// </summary>
    PostBuild,

    /// <summary>
    /// Executed at the very end, after all other operations, including PostBuild plugins.
    /// Useful for cleanup or final notifications.
    /// </summary>
    BuildComplete
}
