// --- HcgBlogGenerator.Core/Models/PluginContext.cs ---

using HcgBlogGenerator.Core.Interfaces;

namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Provides context and services to plugins during various stages of the build process.
/// </summary>
public class PluginContext {
    /// <summary>
    /// The global site configuration.
    /// </summary>
    public SiteConfiguration SiteConfiguration { get; set; }

    /// <summary>
    /// The site context data structure that will eventually be passed to templates.
    /// Plugins can potentially modify collections before final rendering (use with caution).
    /// </summary>
    /// <remarks>
    /// Exposing mutable collections directly can be risky. Consider providing specific methods
    /// for plugins to add/modify data in a controlled way if needed.
    /// For now, providing the context allows read access and potential modification if the
    /// underlying collections in SiteContext are mutable (which they currently might be during build).
    /// </remarks>
    public SiteContext SiteContext { get; set; } // Holds posts, pages, data etc.

    /// <summary>
    /// Provides access to the file system abstraction.
    /// </summary>
    public IFileSystem FileSystem { get; set; }

    /// <summary>
    /// Provides access to the template engine for rendering arbitrary templates (e.g., for RSS feeds).
    /// </summary>
    public ITemplateEngine TemplateEngine { get; set; }

    /// <summary>
    /// Provides access to the Markdown parser.
    /// </summary>
    public IMarkdownParser MarkdownParser { get; set; }
}
