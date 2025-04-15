using HcgBlogGenerator.Core.Models;

namespace HcgBlogGenerator.Core.Abstractions;

/// <summary>
/// Parses raw content (e.g., a Markdown file with frontmatter)
/// into structured data including metadata and HTML body.
/// </summary>
public interface IContentParser {
    /// <summary>
    /// Parses the raw content string.
    /// </summary>
    /// <param name="rawContent">The raw string content (e.g., read from a file).</param>
    /// <param name="sourcePath">The original path of the content, for context/logging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the parsed frontmatter and the rendered HTML content.</returns>
    Task<ContentParseResult> ParseAsync(string rawContent, string sourcePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of parsing content.
/// </summary>
/// <param name="FrontMatter">The extracted frontmatter metadata.</param>
/// <param name="HtmlContent">The processed HTML content body.</param>
public record ContentParseResult(FrontMatter FrontMatter, string HtmlContent);
