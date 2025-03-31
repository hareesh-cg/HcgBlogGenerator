namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Represents the data context passed to the template engine when rendering a page or post.
/// This acts as the top-level object accessible within templates (e.g., via 'site', 'page', 'posts').
/// </summary>
public class TemplateData {
    /// <summary>
    /// Global site configuration and data.
    /// Accessed in templates like `site.title` or `site.data.my_custom_data`.
    /// </summary>
    //[Scriban.Syntax.ScriptMemberIgnore] // Avoid direct serialization if this class itself is serialized elsewhere
    public SiteContext Site { get; set; }

    /// <summary>
    /// The specific content item (Page or Post) currently being rendered.
    /// Accessed in templates like `page.title` or `post.date`.
    /// Note: We use 'Page' as the property name for simplicity, even if it holds a Post.
    /// Templates can access specific post properties if the object is indeed a Post.
    /// </summary>
    //[Scriban.Syntax.ScriptMemberIgnore]
    public ContentItem Page { get; set; } // Renamed from 'CurrentItem' for common convention (like Jekyll's 'page')

    /// <summary>
    /// The rendered HTML content of the current page/post *before* layout application.
    /// Accessed in templates typically via `{{ content }}` (or similar convention defined by layout).
    /// </summary>
    public string Content { get; set; }
}
