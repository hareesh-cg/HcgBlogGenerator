using System.Text.Json.Serialization;

namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Base class representing a piece of content read from the source.
/// </summary>
public abstract class ContentItem {
    /// <summary>
    /// Original source path relative to the content root (e.g., "posts/my-first-post.md").
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Destination path relative to the output root (e.g., "posts/my-first-post/index.html").
    /// This is the path where the file will be written.
    /// </summary>
    public string DestinationPath { get; set; } = string.Empty;

    /// <summary>
    /// The extracted frontmatter metadata.
    /// </summary>
    public FrontMatter FrontMatter { get; set; } = new FrontMatter();

    /// <summary>
    /// The processed HTML content (body), after Markdown conversion.
    /// </summary>
    public string HtmlContent { get; set; } = string.Empty;

    /// <summary>
    /// The final URL path relative to the site root (e.g., "/posts/my-first-post/").
    /// Calculated based on configuration and source path/frontmatter. Used for linking.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the overall site context, useful for accessing global data during processing or templating.
    /// Be cautious about circular references if serializing.
    /// </summary>
    [JsonIgnore] // Avoid serialization issues
    public SiteContext? SiteContext { get; set; }

    /// <summary>
    /// Holds calculated SEO metadata for this content item.
    /// Populated by the SeoPlugin. Null if SEO data wasn't generated.
    /// </summary>
    [JsonIgnore] // Avoid serializing this complex object if ContentItem is ever serialized directly
    public SeoData? Seo { get; set; }
}
