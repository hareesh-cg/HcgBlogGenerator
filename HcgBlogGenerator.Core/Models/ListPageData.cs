using System.Collections.Generic;

namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Represents a dynamically generated page that lists content items,
/// typically used for taxonomy terms (tags/categories) or main index pages.
/// Inherits common properties from ContentItem.
/// </summary>
public class ListPageData : ContentItem {
    /// <summary>
    /// The list of posts to be displayed on this page.
    /// For paginated lists, this will contain only the items for the current page.
    /// </summary>
    public List<PostData> Posts { get; set; } = new List<PostData>();

    /// <summary>
    /// The type of list page (e.g., "Tag", "Category", "Index").
    /// </summary>
    public string ListType { get; set; } = string.Empty;

    /// <summary>
    /// The specific term this page represents, if applicable (e.g., "CSharp" for a tag page).
    /// </summary>
    public string Term { get; set; } = string.Empty;

    /// <summary>
    /// The URL-friendly slug for the term, if applicable.
    /// </summary>
    public string TermSlug { get; set; } = string.Empty;

    /// <summary>
    /// Pagination details for this specific list page.
    /// Null if the list is not paginated.
    /// </summary>
    public Pager<PostData>? PagerInfo { get; set; }
}
