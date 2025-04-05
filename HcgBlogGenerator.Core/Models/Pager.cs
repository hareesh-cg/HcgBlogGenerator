using System.Collections.Generic;

namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Provides information needed to render pagination controls for lists of items (typically posts).
/// </summary>
/// <typeparam name="T">The type of item being paginated (e.g., PostData).</typeparam>
public class Pager<T> where T : class // Renamed from PaginationInfo to Pager, common term
{
    /// <summary>
    /// The items included on the current page.
    /// </summary>
    public List<T> ItemsOnPage { get; set; } = new List<T>();

    /// <summary>
    /// The current page number (1-based).
    /// </summary>
    public int CurrentPage { get; set; }

    /// <summary>
    /// The total number of pages available.
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// The total number of items across all pages.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// The number of items displayed per page.
    /// </summary>
    public int ItemsPerPage { get; set; }

    /// <summary>
    /// The URL path for the previous page, or null if on the first page.
    /// </summary>
    public string? PreviousPageUrl { get; set; }

    /// <summary>
    /// The URL path for the next page, or null if on the last page.
    /// </summary>
    public string? NextPageUrl { get; set; }

    /// <summary>
    /// Base URL path for page numbers (e.g., "/blog/page/" -> generates "/blog/page/2/", "/blog/page/3/").
    /// Page 1 might map to a different base path (e.g., "/blog/").
    /// </summary>
    public string PageUrlTemplate { get; set; } = string.Empty;

    /// <summary>
    /// URL Path for the first page (might be different from the template, e.g. /blog/ vs /blog/page/1/).
    /// </summary>
    public string FirstPageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Helper property to check if there is a previous page.
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;

    /// <summary>
    /// Helper property to check if there is a next page.
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;
}
