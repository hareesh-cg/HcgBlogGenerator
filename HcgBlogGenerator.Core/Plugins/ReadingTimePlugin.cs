using System.Text.RegularExpressions;

using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Models;

using Microsoft.Extensions.Logging;

namespace HcgBlogGenerator.Core.Plugins;

/// <summary>
/// Calculates the estimated reading time for blog posts.
/// </summary>
public class ReadingTimePlugin : IPlugin {
    private readonly ILogger<ReadingTimePlugin> _logger;
    private const int WordsPerMinute = 225; // Average reading speed

    public string Name => "Reading Time Calculator";

    public ReadingTimePlugin(ILogger<ReadingTimePlugin> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task ExecuteAsync(
        PipelineStage stage,
        SiteContext siteContext,
        IFileSystem sourceFileSystem,
        IFileSystem outputFileSystem,
        CancellationToken cancellationToken = default) {
        // This plugin only runs after content processing
        if (stage != PipelineStage.PostContentProcessing) {
            return Task.CompletedTask;
        }

        _logger.LogInformation("Executing {PluginName}...", Name);
        int postsUpdated = 0;

        foreach (var post in siteContext.Posts) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                int readingTime = CalculateReadingTime(post.HtmlContent);
                post.ReadingTimeMinutes = readingTime;
                _logger.LogTrace("Calculated reading time for '{SourcePath}': {ReadingTime} min", post.SourcePath, readingTime);
                postsUpdated++;
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to calculate reading time for post: {SourcePath}", post.SourcePath);
                // Continue with next post
            }
        }

        _logger.LogInformation("{PluginName} finished. Updated {Count} posts.", Name, postsUpdated);
        return Task.CompletedTask; // Calculation is synchronous
    }

    /// <summary>
    /// Calculates reading time based on word count.
    /// </summary>
    /// <param name="htmlContent">The HTML content of the post.</param>
    /// <returns>Estimated reading time in minutes (rounded up).</returns>
    private int CalculateReadingTime(string htmlContent) {
        if (string.IsNullOrWhiteSpace(htmlContent)) {
            return 0;
        }

        // 1. Strip HTML tags to get plain text
        string plainText = Regex.Replace(htmlContent, "<.*?>", string.Empty);

        // 2. Count words (simple split by space/punctuation)
        // More sophisticated word counting might be needed for different languages.
        // Match words: sequences of letters, numbers, or apostrophes within words.
        MatchCollection wordMatches = Regex.Matches(plainText, @"[\w'-]+");
        int wordCount = wordMatches.Count;


        // 3. Calculate time
        if (wordCount == 0) {
            return 0;
        }

        double minutes = (double)wordCount / WordsPerMinute;

        // Round up to the nearest minute
        return (int)Math.Ceiling(minutes);
    }
}
