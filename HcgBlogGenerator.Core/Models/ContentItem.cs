namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Abstract base class for content items like pages and posts
/// that are generated from source files (e.g., Markdown).
/// </summary>
public abstract class ContentItem
{
    /// <summary>
    /// The absolute path to the original source file.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// The absolute path where the processed output file will be written.
    /// </summary>
    public required string OutputPath { get; init; }

    /// <summary>
    /// The relative URL for this content item within the generated site (e.g., "/about/", "/posts/my-first-post.html").
    /// Starts with a '/'.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// The parsed front matter associated with this content item.
    /// </summary>
    public required FrontMatter FrontMatter { get; init; }

    /// <summary>
    /// The raw content of the source file *after* the front matter has been removed.
    /// </summary>
    public required string RawContent { get; init; }

    /// <summary>
    /// The final HTML content after processing Markdown and applying layouts/templates.
    /// This is typically set during the rendering phase.
    /// </summary>
    public string HtmlContent { get; set; } = string.Empty;

    /// <summary>
    /// A reference to the global site configuration. Useful for accessing site-wide data within templates.
    /// </summary>
    public required SiteConfiguration Site { get; init; }

    // Common properties derived from FrontMatter for easier access in templates
    // These can be overridden by specific content types (like Post) if needed.

    /// <summary>
    /// The title of the content item, usually derived from front matter.
    /// </summary>
    public virtual string? Title => FrontMatter.Title;

    /// <summary>
    /// The date associated with the content item, usually derived from front matter.
    /// </summary>
    public virtual DateTime? Date => FrontMatter.Date;

    /// <summary>
    /// The layout specified in the front matter.
    /// </summary>
    public virtual string? Layout => FrontMatter.Layout;

    /// <summary>
    /// The excerpt or summary, usually derived from front matter.
    /// </summary>
    public virtual string? Excerpt => FrontMatter.Excerpt;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItem"/> class.
    /// Protected constructor forces use through derived classes.
    /// </summary>
    protected ContentItem() { }
} 