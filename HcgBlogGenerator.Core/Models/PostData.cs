using System;

namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Represents a blog post.
/// Inherits common properties from ContentItem and adds post-specific details.
/// </summary>
public class PostData : ContentItem {
    /// <summary>
    /// The publication date of the post, primarily sourced from FrontMatter.Date.
    /// Made non-nullable here as posts typically require a date. Validation should enforce this.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Estimated reading time in minutes. Calculated by a plugin/service.
    /// </summary>
    public int ReadingTimeMinutes { get; set; }

    /// <summary>
    /// Generated summary or excerpt for display in listings.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the next post chronologically (or null if newest).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore] // Avoid serialization issues
    public PostData? NextPost { get; set; }

    /// <summary>
    /// Reference to the previous post chronologically (or null if oldest).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore] // Avoid serialization issues
    public PostData? PreviousPost { get; set; }

    // Potentially add:
    // public List<PostData> RelatedPosts { get; set; } = new List<PostData>();
}
