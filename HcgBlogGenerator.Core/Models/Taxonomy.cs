namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Represents a single taxonomy term (e.g., a specific category or tag) and the posts associated with it.
/// </summary>
public class TaxonomyTerm {
    /// <summary>
    /// The name of the term (e.g., "CSharp", "Tutorials").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The URL-friendly slug for this term (e.g., "csharp", "tutorials").
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// The full URL path for the page listing posts under this term (e.g., "/tags/csharp/").
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// List of posts associated with this term.
    /// </summary>
    public List<PostData> Posts { get; set; } = new List<PostData>();

    /// <summary>
    /// The type of taxonomy this term belongs to (e.g., "category", "tag").
    /// </summary>
    public string TaxonomyType { get; set; } = string.Empty;
}
