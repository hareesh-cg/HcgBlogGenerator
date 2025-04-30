using System.Text; // Required for StringBuilder potentially

using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Models;

using Microsoft.Extensions.Logging;

using YamlDotNet.Core; // For YamlException
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Implements IContentParser for HTML files containing optional YAML frontmatter.
/// Extracts frontmatter and preserves the remaining HTML content (including any Scriban tags) as the body.
/// Does NOT process or render Scriban tags at this stage.
/// </summary>
public class HtmlContentParser : IContentParser {
    private readonly ILogger<HtmlContentParser> _logger;
    private readonly IDeserializer _yamlDeserializer;
    private const string FrontMatterDelimiter = "---";

    public HtmlContentParser(ILogger<HtmlContentParser> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure YamlDotNet Deserializer (same config as MarkdigContentParser for consistency)
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithTypeConverter(new DateTimeYamlConverter()) // Use the same converter
            .Build();

        _logger.LogDebug("HtmlContentParser initialized.");
    }

    /// <summary>
    /// Parses the raw HTML content string containing potential YAML frontmatter.
    /// </summary>
    /// <param name="rawContent">The raw string content of the HTML file.</param>
    /// <param name="sourcePath">The original path of the content, for context/logging.</param>
    /// <param name="cancellationToken">Cancellation token (not actively used in this sync implementation).</param>
    /// <returns>A Task resulting in ContentParseResult containing FrontMatter and the HTML body (with Scriban tags intact).</returns>
    public Task<ContentParseResult> ParseAsync(string rawContent, string sourcePath, CancellationToken cancellationToken = default) {
        _logger.LogDebug("Parsing HTML content from source: {SourcePath}", sourcePath);

        if (string.IsNullOrWhiteSpace(rawContent)) {
            _logger.LogWarning("Raw content for {SourcePath} is empty or whitespace.", sourcePath);
            return Task.FromResult(new ContentParseResult(new FrontMatter(), string.Empty));
        }

        FrontMatter frontMatter = new FrontMatter();
        string htmlBody = rawContent; // Default to whole content if no frontmatter found

        try {
            // --- Check for and Extract Frontmatter ---
            if (rawContent.StartsWith(FrontMatterDelimiter)) {
                // Find the end of the first line (after the opening delimiter)
                int firstDelimiterEnd = rawContent.IndexOf('\n');
                if (firstDelimiterEnd > 0) // Ensure there's content after the first delimiter line
                {
                    // Find the closing delimiter starting from the next line
                    int closingDelimiterStart = rawContent.IndexOf(FrontMatterDelimiter, firstDelimiterEnd + 1);

                    if (closingDelimiterStart > 0) {
                        // Extract YAML content (between the delimiters, excluding the delimiter lines themselves)
                        // Start after the first line's newline, length up to the start of the closing delimiter
                        string yamlContent = rawContent.Substring(firstDelimiterEnd + 1, closingDelimiterStart - (firstDelimiterEnd + 1)).Trim();

                        // Extract the body content (after the closing delimiter line)
                        // Find the end of the closing delimiter line
                        int closingDelimiterEnd = rawContent.IndexOf('\n', closingDelimiterStart);
                        htmlBody = (closingDelimiterEnd > 0 && closingDelimiterEnd < rawContent.Length - 1)
                                     ? rawContent.Substring(closingDelimiterEnd + 1).TrimStart() // Trim leading whitespace/newlines from body
                                     : string.Empty; // No content after closing delimiter

                        _logger.LogTrace("Found frontmatter block in {SourcePath}.", sourcePath);

                        // --- Deserialize YAML ---
                        if (!string.IsNullOrWhiteSpace(yamlContent)) {
                            try {
                                frontMatter = _yamlDeserializer.Deserialize<FrontMatter>(yamlContent) ?? new FrontMatter();
                                _logger.LogTrace("Successfully deserialized frontmatter for {SourcePath}", sourcePath);
                            }
                            catch (YamlException yamlEx) {
                                _logger.LogError(yamlEx, "YAML parsing failed for {SourcePath}. YAML content:\n---\n{YamlContent}\n---", sourcePath, yamlContent);
                                frontMatter = new FrontMatter(); // Reset to default on error
                            }
                            catch (Exception ex) // Catch other potential deserialization errors
                            {
                                _logger.LogError(ex, "Unexpected error deserializing YAML for {SourcePath}.", sourcePath);
                                frontMatter = new FrontMatter();
                            }
                        }
                        else {
                            _logger.LogTrace("Frontmatter block found but was empty for {SourcePath}.", sourcePath);
                        }
                    }
                    else {
                        // Opening delimiter found, but no closing one. Treat all as body.
                        _logger.LogWarning("Found opening frontmatter delimiter '---' but no closing delimiter in {SourcePath}. Treating entire file as HTML body.", sourcePath);
                        htmlBody = rawContent; // Keep the original raw content as body
                    }
                }
                else {
                    // Starts with --- but nothing else? Treat as body.
                    _logger.LogWarning("File {SourcePath} starts with '---' but has no subsequent content or closing delimiter. Treating entire file as HTML body.", sourcePath);
                    htmlBody = rawContent;
                }
            }
            else {
                _logger.LogTrace("No frontmatter block found (doesn't start with '---') in {SourcePath}.", sourcePath);
                // htmlBody already defaults to rawContent
            }

            _logger.LogDebug("Finished parsing HTML content for {SourcePath}.", sourcePath);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Unexpected error during HTML content parsing for {SourcePath}", sourcePath);
            // Return default content to avoid breaking build if possible
            return Task.FromResult(new ContentParseResult(new FrontMatter(), string.Empty));
        }

        // Return the result - HtmlContent contains the original HTML body WITH Scriban tags
        return Task.FromResult(new ContentParseResult(frontMatter, htmlBody));
    }
}
