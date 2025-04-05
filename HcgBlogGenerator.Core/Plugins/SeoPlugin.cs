using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Models;

using Microsoft.Extensions.Logging;

using System;
using System.Linq;
using System.Net; // For WebUtility.HtmlDecode
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HcgBlogGenerator.Core.Plugins;

/// <summary>
/// Populates SEO metadata (Title, Description, OG, Twitter) for content items.
/// </summary>
public class SeoPlugin : IPlugin {
    private readonly ILogger<SeoPlugin> _logger;
    public string Name => "SEO Metadata Generator";

    // Simple regex to find first image src. Limitation: Doesn't handle complex cases, relative paths well.
    private static readonly Regex FindFirstImageRegex = new Regex("<img[^>]+src\\s*=\\s*['\"]([^'\"]+)['\"][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // Simple regex to strip tags for description truncation
    private static readonly Regex StripTagsRegex = new Regex("<.*?>", RegexOptions.Compiled);


    // Max lengths for descriptions
    private const int MaxMetaDescriptionLength = 160;
    private const int MaxOgDescriptionLength = 200; // Slightly longer often acceptable
    private const int MaxTwitterDescriptionLength = 200;

    public SeoPlugin(ILogger<SeoPlugin> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task ExecuteAsync(
        PipelineStage stage,
        SiteContext siteContext,
        IFileSystem sourceFileSystem, // Not typically used here
        IFileSystem outputFileSystem, // Not typically used here
        CancellationToken cancellationToken = default) {
        if (stage != PipelineStage.PostContentProcessing) {
            return Task.CompletedTask; // Run before rendering
        }

        _logger.LogInformation("Executing {PluginName}...", Name);
        var config = siteContext.Configuration;
        string baseUrl = config.BaseUrl?.TrimEnd('/') ?? string.Empty;

        if (string.IsNullOrEmpty(baseUrl)) {
            _logger.LogWarning("{PluginName}: BaseUrl is not configured. Cannot generate absolute URLs for SEO tags. Some tags will be omitted or incorrect.", Name);
            // Continue, but URLs will be relative or missing
        }

        var allContent = siteContext.Posts.Cast<ContentItem>().Concat(siteContext.Pages.Cast<ContentItem>());
        int itemsProcessed = 0;

        foreach (var item in allContent) {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(item.Url)) continue; // Need a URL

            try {
                var seo = new SeoData();
                item.Seo = seo; // Attach to item

                bool isPost = item is PostData;
                bool isHomePage = item.Url == "/"; // Simple homepage check

                // --- Determine Base URL Dependent Values ---
                seo.CanonicalUrl = GetAbsoluteUrl(baseUrl, item.Url);
                seo.OgUrl = seo.CanonicalUrl; // Usually the same

                // --- Determine Titles ---
                string pageTitle = item.FrontMatter.Title ?? config.Title ?? "Untitled";
                seo.Title = item.FrontMatter.Get<string>("seoTitle") // Allow frontmatter override
                            ?? (isHomePage ? config.Title : $"{pageTitle} | {config.Title}"); // Append site title usually, except for home
                seo.OgTitle = item.FrontMatter.Get<string>("ogTitle") ?? pageTitle; // OG title usually doesn't include site name
                seo.TwitterTitle = item.FrontMatter.Get<string>("twitterTitle") ?? seo.OgTitle; // Default Twitter title to OG Title

                // --- Determine Descriptions ---
                // Start with explicit metaDescription from frontmatter
                string baseDescription = item.FrontMatter.Get<string>("metaDescription")
                                        // Fallback to item summary (could be explicit or generated)
                                        ?? (item as PostData)?.Summary // Check PostData.Summary first
                                        ?? item.FrontMatter.Summary // Then FrontMatter.Summary
                                                                    // Fallback to site description
                                        ?? config.Description
                                        ?? string.Empty;

                seo.MetaDescription = TruncateText(CleanText(baseDescription), MaxMetaDescriptionLength);
                seo.OgDescription = TruncateText(CleanText(item.FrontMatter.Get<string>("ogDescription") ?? baseDescription), MaxOgDescriptionLength);
                seo.TwitterDescription = TruncateText(CleanText(item.FrontMatter.Get<string>("twitterDescription") ?? baseDescription), MaxTwitterDescriptionLength);

                // --- Determine Image ---
                string? imageSource = item.FrontMatter.Get<string>("ogImage")
                                   ?? item.FrontMatter.Get<string>("twitterImage")
                                   ?? item.FrontMatter.Get<string>("image")
                                   ?? FindFirstImageUrl(item.HtmlContent)
                                   ?? (config.ExtraData.TryGetValue("defaultOgImage", out var defImg) ? defImg as string : null); // Default from config

                seo.OgImage = GetAbsoluteUrl(baseUrl, imageSource); // Make absolute if needed
                seo.TwitterImage = GetAbsoluteUrl(baseUrl, item.FrontMatter.Get<string>("twitterImage") ?? imageSource); // Allow specific twitter image override

                // --- Determine OG Type ---
                seo.OgType = item.FrontMatter.Get<string>("ogType") ?? (isPost ? "article" : "website");

                // --- Determine Locale ---
                seo.OgLocale = ConvertToOgLocale(config.Language); // Convert "en-US" to "en_US"

                // --- Article Specific ---
                if (item is PostData post) {
                    seo.ArticlePublishedTime = post.Date.ToString("o"); // ISO 8601
                    if (post.FrontMatter.LastModified.HasValue && post.FrontMatter.LastModified != post.Date) {
                        seo.ArticleModifiedTime = post.FrontMatter.LastModified.Value.ToString("o");
                    }
                    // Collect tags for article:tag
                    seo.ArticleTags = post.FrontMatter.Tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList();
                }

                // --- Twitter Specific ---
                seo.TwitterCard = item.FrontMatter.Get<string>("twitterCard") // Allow override
                                 ?? (!string.IsNullOrWhiteSpace(seo.TwitterImage) ? "summary_large_image" : "summary"); // Base decision on image presence
                seo.TwitterSite = config.ExtraData.TryGetValue("twitterHandle", out var handle) ? FormatTwitterHandle(handle as string) : null;
                // Get creator from frontmatter 'authorTwitter' or fallback to site handle
                string? creatorHandle = item.FrontMatter.Get<string>("authorTwitter")
                                       ?? (config.ExtraData.TryGetValue("defaultAuthorTwitter", out var defAuthHandle) ? defAuthHandle as string : null);
                seo.TwitterCreator = FormatTwitterHandle(creatorHandle) ?? seo.TwitterSite; // Default creator to site if specific not found


                itemsProcessed++;
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to generate SEO metadata for item: {SourcePath}", item.SourcePath);
                item.Seo = null; // Ensure Seo property is null if processing failed
            }
        }

        _logger.LogInformation("{PluginName} finished. Processed SEO metadata for {Count} items.", Name, itemsProcessed);
        return Task.CompletedTask; // Processing is synchronous
    }

    // --- Helper Methods ---

    private string? GetAbsoluteUrl(string baseUrl, string? relativeOrAbsoluteUrl) {
        if (string.IsNullOrWhiteSpace(relativeOrAbsoluteUrl)) return null;
        if (string.IsNullOrWhiteSpace(baseUrl)) return relativeOrAbsoluteUrl; // Can't make absolute

        // Check if already absolute
        if (Uri.TryCreate(relativeOrAbsoluteUrl, UriKind.Absolute, out _)) {
            return relativeOrAbsoluteUrl;
        }

        // Combine with base URL - ensure no double slashes
        return baseUrl.TrimEnd('/') + "/" + relativeOrAbsoluteUrl.TrimStart('/');
    }

    private string? FindFirstImageUrl(string htmlContent) {
        if (string.IsNullOrWhiteSpace(htmlContent)) return null;
        var match = FindFirstImageRegex.Match(htmlContent);
        return match.Success ? match.Groups[1].Value : null;
    }

    private string CleanText(string? text) {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        // Strip tags first
        string cleaned = StripTagsRegex.Replace(text, string.Empty);
        // Decode HTML entities
        cleaned = WebUtility.HtmlDecode(cleaned); // Use System.Net.WebUtility
                                                  // Normalize whitespace
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        return cleaned;
    }

    private string TruncateText(string text, int maxLength) {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;

        // Basic truncation - could be improved to respect word boundaries
        return text.Substring(0, maxLength - 3) + "..."; // Reserve space for ellipsis
    }

    private string? FormatTwitterHandle(string? handle) {
        if (string.IsNullOrWhiteSpace(handle)) return null;
        handle = handle.Trim();
        return handle.StartsWith("@") ? handle : "@" + handle;
    }

    private string? ConvertToOgLocale(string? languageCode) {
        // Convert IETF language tag (e.g., "en-US") to OG locale format (e.g., "en_US")
        if (string.IsNullOrWhiteSpace(languageCode)) return null;
        return languageCode.Replace('-', '_');
    }
}
