using HcgBlogGenerator.Core.Interfaces;
using Markdig;
using Microsoft.Extensions.Logging;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Parses Markdown text into HTML using the Markdig library.
/// </summary>
public class MarkdownParser : IMarkdownParser
{
    private readonly ILogger<MarkdownParser> _logger;
    private readonly MarkdownPipeline _pipeline;

    public MarkdownParser(ILogger<MarkdownParser> logger)
    {
        _logger = logger;

        // Configure the Markdig pipeline with desired extensions.
        // 'Advanced' enables many common features like pipe tables, footnotes, etc.
        // Add other extensions as needed (e.g., syntax highlighting, diagrams).
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions() // Includes PipeTables, EmphasisExtras, TaskLists, DefinitionLists, Footnotes, AutoLinks, etc.
            .UseYamlFrontMatter()    // Although we parse front matter separately, this prevents Markdig from rendering it as text.
            .UseEmojiAndSmiley()     // Enable 😊 emojis and :) smileys
            .UseAutoIdentifiers()
            .UseTaskLists()
            .UseSyntaxHighlighting()
            .Build();

        _logger.LogDebug("Markdig pipeline configured with advanced extensions.");
    }

    /// <inheritdoc />
    public string ConvertToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        try
        {
            var html = Markdown.ToHtml(markdown, _pipeline);
            // _logger.LogTrace("Markdown converted to HTML successfully."); // Potentially too verbose
            return html;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting Markdown to HTML.");
            // Depending on requirements, you might want to return the original markdown,
            // an error message, or re-throw the exception. Returning empty for now.
            return string.Empty;
        }
    }
} 
