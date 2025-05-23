using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Models;

using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;

using Microsoft.Extensions.Logging;

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Implements IContentParser using the Markdig library for Markdown processing
/// and YamlDotNet for frontmatter deserialization.
/// </summary>
public class MarkdigContentParser : IContentParser
{
    private readonly ILogger<MarkdigContentParser> _logger;
    private readonly MarkdownPipeline _pipeline;
    private readonly IDeserializer _yamlDeserializer;

    public MarkdigContentParser(ILogger<MarkdigContentParser> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure Markdig pipeline
        // Enable commonly used extensions. Consider making this configurable later.
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions() // Includes PipeTables, Footnotes, TaskLists, etc.
            .UseYamlFrontMatter()    // Essential for extracting frontmatter block
            .UseDiagrams()           // Mermaid, Nomnoml diagrams
            .UseAutoIdentifiers()    // Generate IDs for headings automatically
                                     // .UseEmphasisExtras()  // Strikethrough, Subscript, Superscript, etc.
                                     // .UseGenericAttributes() // Allow adding HTML attributes like { #id .class }
            .Build();

        // Configure YamlDotNet Deserializer
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance) // Match typical YAML keys (e.g., readingTime) to C# properties (ReadingTime)
            .IgnoreUnmatchedProperties() // Avoid errors if frontmatter has extra keys not in our model
            .WithTypeConverter(new DateTimeYamlConverter()) // Handle DateTime correctly
            .Build();

        _logger.LogDebug("MarkdigContentParser initialized with configured pipeline.");
    }

    /// <summary>
    /// Parses the raw content string containing potential YAML frontmatter and Markdown.
    /// </summary>
    /// <param name="rawContent">The raw string content.</param>
    /// <param name="sourcePath">The original path of the content, for context/logging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Task resulting in ContentParseResult containing FrontMatter and HTML.</returns>
    public Task<ContentParseResult> ParseAsync(string rawContent, string sourcePath, CancellationToken cancellationToken = default) {
        _logger.LogDebug("Parsing content from source: {SourcePath}", sourcePath);

        FrontMatter frontMatter = new FrontMatter(); // Default empty frontmatter
        string htmlContent = string.Empty;

        try {
            var writer = new StringWriter(); // Use StringWriter to capture HTML output
            var document = Markdown.Parse(rawContent, _pipeline);

            // --- Extract and Deserialize FrontMatter ---
            var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
            if (yamlBlock != null) {
                // Extract the YAML content lines
                // Note: yamlBlock.Lines contains StringLine objects. Need to reconstruct the YAML string.
                var yamlContent = yamlBlock.Lines.ToString(); // This handles multi-line correctly

                if (!string.IsNullOrWhiteSpace(yamlContent)) {
                    try {
                        frontMatter = _yamlDeserializer.Deserialize<FrontMatter>(yamlContent) ?? new FrontMatter();
                        _logger.LogTrace("Successfully deserialized frontmatter for {SourcePath}", sourcePath);
                    }
                    catch (YamlException yamlEx) {
                        _logger.LogError(yamlEx, "YAML parsing failed for {SourcePath}. YAML content:\n---\n{YamlContent}\n---", sourcePath, yamlContent);
                        // Decide on behavior: throw, return default, or partially continue?
                        // Let's continue with default frontmatter but log error.
                        frontMatter = new FrontMatter(); // Reset to default
                    }
                    catch (Exception ex) {
                        _logger.LogError(ex, "Unexpected error deserializing YAML for {SourcePath}. YAML content:\n---\n{YamlContent}\n---", sourcePath, yamlContent);
                        frontMatter = new FrontMatter(); // Reset to default
                    }
                }
                else {
                    _logger.LogTrace("No YAML content found within frontmatter block for {SourcePath}", sourcePath);
                }
            }
            else {
                _logger.LogTrace("No YAML frontmatter block found in {SourcePath}", sourcePath);
            }


            // --- Render Markdown to HTML ---
            // Markdown.ToHtml expects the raw string or the parsed document
            htmlContent = Markdown.ToHtml(document, _pipeline);

            _logger.LogDebug("Successfully parsed Markdown and generated HTML for {SourcePath}", sourcePath);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error parsing Markdown or rendering HTML for {SourcePath}", sourcePath);
            // Depending on desired robustness, you might want to return a result
            // with empty content/default frontmatter or re-throw the exception.
            // Returning default allows potentially skipping problematic files.
            return Task.FromResult(new ContentParseResult(new FrontMatter(), string.Empty));
            // Or rethrow: throw;
        }

        // Return the combined result
        return Task.FromResult(new ContentParseResult(frontMatter, htmlContent));
    }
}
