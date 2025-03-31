namespace HcgBlogGenerator.Core.Interfaces;

/// <summary>
/// Defines the contract for parsing Markdown text into HTML.
/// </summary>
public interface IMarkdownParser
{
    /// <summary>
    /// Converts the provided Markdown string to HTML.
    /// </summary>
    /// <param name="markdown">The Markdown text to convert.</param>
    /// <returns>The resulting HTML string.</returns>
    string ConvertToHtml(string markdown);
} 