namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Holds calculated SEO metadata for a specific content item.
/// </summary>
public class SeoData {
    /// <summary>
    /// The final calculated content for the <title> tag.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// The content for the <meta name="description"> tag.
    /// </summary>
    public string? MetaDescription { get; set; }

    /// <summary>
    /// The absolute canonical URL for the page/post.
    /// </summary>
    public string? CanonicalUrl { get; set; }

    // --- Open Graph ---
    /// <summary>
    /// Open Graph type (e.g., "website", "article").
    /// </summary>
    public string? OgType { get; set; }

    /// <summary>
    /// Open Graph URL (usually same as CanonicalUrl).
    /// </summary>
    public string? OgUrl { get; set; }

    /// <summary>
    /// Open Graph title.
    /// </summary>
    public string? OgTitle { get; set; }

    /// <summary>
    /// Open Graph description.
    /// </summary>
    public string? OgDescription { get; set; }

    /// <summary>
    /// Absolute URL for the main Open Graph image.
    /// </summary>
    public string? OgImage { get; set; }

    /// <summary>
    /// Optional: Width of the Open Graph image.
    /// </summary>
    public int? OgImageWidth { get; set; } // Not calculated by this basic plugin

    /// <summary>
    /// Optional: Height of the Open Graph image.
    /// </summary>
    public int? OgImageHeight { get; set; } // Not calculated by this basic plugin

    /// <summary>
    /// Open Graph locale (e.g., "en_US").
    /// </summary>
    public string? OgLocale { get; set; }

    /// <summary>
    /// ISO 8601 formatted publication time (for article type).
    /// </summary>
    public string? ArticlePublishedTime { get; set; }

    /// <summary>
    /// ISO 8601 formatted modification time (for article type).
    /// </summary>
    public string? ArticleModifiedTime { get; set; }

    /// <summary>
    /// Optional: Article tags.
    /// </summary>
    public List<string>? ArticleTags { get; set; }


    // --- Twitter Card ---
    /// <summary>
    /// Type of Twitter card ("summary", "summary_large_image").
    /// </summary>
    public string? TwitterCard { get; set; }

    /// <summary>
    /// The Twitter handle of the site (e.g., "@username").
    /// </summary>
    public string? TwitterSite { get; set; }

    /// <summary>
    /// Optional: The Twitter handle of the content creator (e.g., "@username").
    /// </summary>
    public string? TwitterCreator { get; set; }

    /// <summary>
    /// Title for the Twitter card.
    /// </summary>
    public string? TwitterTitle { get; set; }

    /// <summary>
    /// Description for the Twitter card.
    /// </summary>
    public string? TwitterDescription { get; set; }

    /// <summary>
    /// Absolute URL for the Twitter card image.
    /// </summary>
    public string? TwitterImage { get; set; }

}
