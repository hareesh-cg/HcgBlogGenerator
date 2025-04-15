using System.Text.RegularExpressions;
using System.Web;

using HcgBlogGenerator.Core.Abstractions;

using Microsoft.Extensions.Logging;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Basic implementation of IMetadataExtractor.
/// </summary>
public class MetadataExtractor : IMetadataExtractor {
    private readonly ILogger<MetadataExtractor> _logger;

    // Regex to find the content within the first <p> tag. Handles attributes on the tag.
    private static readonly Regex FirstParagraphRegex = new Regex(@"<p(?:\s+[^>]*?)?>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    // Simple regex to strip all tags
    private static readonly Regex StripTagsRegex = new Regex("<.*?>", RegexOptions.Compiled);

    public MetadataExtractor(ILogger<MetadataExtractor> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string? GenerateSummary(string htmlContent, int maxLength = 250) {
        if (string.IsNullOrWhiteSpace(htmlContent)) return null;

        try {
            // TODO: Implement <!--more--> tag splitting logic here if desired

            // Strategy: Use the first paragraph
            var firstParagraphMatch = FirstParagraphRegex.Match(htmlContent);
            string textToSummarize = firstParagraphMatch.Success
                ? firstParagraphMatch.Groups[1].Value // Content of the first <p> tag
                : htmlContent; // Fallback: use the beginning of the full content

            // Strip ALL HTML tags from the selected text
            string plainText = StripTagsRegex.Replace(textToSummarize, string.Empty).Trim();

            // Decode HTML entities (e.g., & -> &)
            // Note: Requires reference to System.Web (via <FrameworkReference Include="Microsoft.AspNetCore.App" /> in csproj)
            // or use a different HTML decoding library if preferred.
            plainText = HttpUtility.HtmlDecode(plainText);

            // Normalize whitespace (replace multiple spaces/newlines with single space)
            plainText = Regex.Replace(plainText, @"\s+", " ").Trim();

            if (string.IsNullOrWhiteSpace(plainText)) return null; // No text content found

            // Truncate if necessary
            if (plainText.Length <= maxLength) {
                return plainText;
            }

            // Attempt to truncate at the last sentence end before maxLength
            int cutoff = plainText.LastIndexOfAny(new[] { '.', '!', '?' }, maxLength - 1);
            // Ensure cutoff is reasonably close to maxLength to avoid tiny summaries
            if (cutoff > maxLength * 0.5) // Heuristic: sentence end must be after 50% of maxLength
            {
                return plainText.Substring(0, cutoff + 1).Trim();
            }

            // Fallback: truncate at nearest space before maxLength
            cutoff = plainText.LastIndexOf(' ', maxLength - 1, Math.Min(maxLength, plainText.Length) - 1);
            if (cutoff > 0) {
                return plainText.Substring(0, cutoff).Trim() + "..."; // Add ellipsis
            }

            // Hard truncate if no suitable space/sentence found
            return plainText.Substring(0, maxLength).Trim() + "...";
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to generate summary from HTML content.");
            return null; // Return null on error
        }
    }
}
