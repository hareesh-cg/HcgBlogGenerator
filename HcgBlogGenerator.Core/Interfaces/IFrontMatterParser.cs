using HcgBlogGenerator.Core.Models;

namespace HcgBlogGenerator.Core.Interfaces;

/// <summary>
/// Defines the contract for parsing YAML front matter from text content.
/// </summary>
public interface IFrontMatterParser
{
    /// <summary>
    /// Parses the YAML front matter from the beginning of the provided text content.
    /// </summary>
    /// <param name="content">The full text content, potentially starting with YAML front matter delimited by '---'.</param>
    /// <returns>
    /// A tuple containing:
    /// - The parsed <see cref="FrontMatter"/> object (or a default object if no front matter is found or parsing fails).
    /// - The remaining content string after the front matter block (or the original content if no front matter is found).
    /// </returns>
    (FrontMatter FrontMatter, string Content) Parse(string content);
} 