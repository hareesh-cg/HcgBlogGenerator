// --- HcgBlogGenerator.Core/Interfaces/IContentProcessor.cs ---

using HcgBlogGenerator.Core.Models;

namespace HcgBlogGenerator.Core.Interfaces;

/// <summary>
/// Defines the contract for processing a single content item (like a Page or Post).
/// This typically involves parsing Markdown, applying templates/layouts, and generating the final HTML.
/// </summary>
public interface IContentProcessor {
    /// <summary>
    /// Processes a given content item.
    /// </summary>
    /// <param name="item">The content item (Page or Post) to process.</param>
    /// <param name="layouts">A collection of available layouts, keyed by layout name.</param>
    /// <param name="siteContext">The global site context data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous processing operation.
    /// The task result is the fully rendered HTML content for the item,
    /// or null if processing fails or the item should be skipped (e.g., draft not included).
    /// The HtmlContent property on the input 'item' should also be updated upon successful processing.
    /// </returns>
    Task<string?> ProcessAsync(
        ContentItem item,
        IReadOnlyDictionary<string, Layout> layouts,
        SiteContext siteContext,
        CancellationToken cancellationToken = default);
}
