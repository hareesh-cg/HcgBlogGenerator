// --- HcgBlogGenerator.Core/Interfaces/ITemplateRenderer.cs ---

using HcgBlogGenerator.Core.Models;

namespace HcgBlogGenerator.Core.Interfaces;

/// <summary>
/// Defines the contract for rendering content within a specified layout template.
/// </summary>
public interface ITemplateRenderer {
    /// <summary>
    /// Renders the final HTML output by applying a layout template to the provided content and data.
    /// </summary>
    /// <param name="layout">The layout template to use.</param>
    /// <param name="templateData">The data context containing site info, current page/post, and the pre-rendered content.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous rendering operation.
    /// The task result contains the final rendered HTML string, or null if rendering fails.
    /// </returns>
    Task<string?> RenderAsync(Layout layout, TemplateData templateData, CancellationToken cancellationToken = default);
}
