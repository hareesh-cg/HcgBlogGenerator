using System.Text;
using System.Xml;
using System.Xml.Linq;

using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Models;

using Microsoft.Extensions.Logging;

namespace HcgBlogGenerator.Core.Plugins;

/// <summary>
/// Generates an RSS 2.0 feed file (feed.xml) for the site's blog posts using System.Xml.Linq.
/// </summary>
public class RssPlugin : IPlugin {
    private readonly ILogger<RssPlugin> _logger;
    public string Name => "RSS Feed Generator (Linq)";

    public RssPlugin(ILogger<RssPlugin> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(
        PipelineStage stage,
        SiteContext siteContext,
        IFileSystem sourceFileSystem, // Not used directly here
        IFileSystem outputFileSystem,
        CancellationToken cancellationToken = default) {
        if (stage != PipelineStage.PostBuild) {
            return; // Only run after the main site files are built
        }

        var rssConfig = siteContext.Configuration.Rss; // Get RSS specific config

        // Check if enabled and prerequisites met
        if (!rssConfig.Enabled) {
            _logger.LogInformation("{PluginName}: RSS feed generation is disabled in configuration. Skipping.", Name);
            return;
        }
        if (string.IsNullOrWhiteSpace(siteContext.Configuration.BaseUrl)) {
            _logger.LogWarning("{PluginName}: BaseUrl is not configured. Cannot generate absolute URLs for RSS feed. Skipping.", Name);
            return;
        }
        if (!siteContext.Posts.Any()) {
            _logger.LogInformation("{PluginName}: No posts found to include in the RSS feed.", Name);
            return;
        }

        _logger.LogInformation("Executing {PluginName}...", Name);
        string outputPath = rssConfig.OutputPath?.Trim('/') ?? "feed.xml"; // Use config path or default

        try {
            XDocument feedDocument = CreateFeedXml(siteContext);

            // Write feed to file using XmlWriter for controlled output
            using (var memoryStream = new MemoryStream()) {
                var writerSettings = new XmlWriterSettings {
                    Async = true,
                    Encoding = new UTF8Encoding(false), // UTF-8 without BOM
                    Indent = true,
                    NewLineHandling = NewLineHandling.Replace,
                };

                using (var writer = XmlWriter.Create(memoryStream, writerSettings)) {
                    await feedDocument.WriteToAsync(writer, cancellationToken);
                    await writer.FlushAsync();
                }

                memoryStream.Position = 0;

                _logger.LogDebug("Writing RSS feed XML content to {OutputPath}", outputPath);
                await outputFileSystem.WriteStreamAsync(outputPath, memoryStream, cancellationToken);
            }

            _logger.LogInformation("{PluginName} finished successfully. Generated RSS feed.", Name);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error generating or writing RSS feed file to {OutputPath}", outputPath);
        }
    }

    private XDocument CreateFeedXml(SiteContext siteContext) {
        var config = siteContext.Configuration;
        var rssConfig = config.Rss;
        string baseUrl = config.BaseUrl.TrimEnd('/');

        XNamespace contentNs = "http://purl.org/rss/1.0/modules/content/"; // Namespace for content:encoded if needed

        // Select and limit posts (already sorted date descending)
        var postsForFeed = siteContext.Posts
                                      .Take(rssConfig.MaxItems > 0 ? rssConfig.MaxItems : int.MaxValue)
                                      .ToList();

        var rssRoot = new XElement("rss",
            new XAttribute("version", "2.0"),
            // Example of adding a namespace for content:encoded if you plan to use it
            // new XAttribute(XNamespace.Xmlns + "content", contentNs),
            new XElement("channel",
                new XElement("title", config.Title),
                new XElement("link", baseUrl), // Link to the website homepage
                new XElement("description", config.Description),
                new XElement("language", config.Language),
                // Use RFC 1123 format ("R") which is standard for RSS/HTTP
                new XElement("lastBuildDate", (postsForFeed.FirstOrDefault()?.Date ?? DateTimeOffset.UtcNow).ToString("R")),
                new XElement("generator", "HcgBlogGenerator"), // Optional generator info
                                                               // Optional: <ttl>, <copyright>, <image> etc.

                // Map posts to <item> elements
                postsForFeed.Select(post => CreateItemElement(post, baseUrl, siteContext))
            )
        );

        return new XDocument(
             new XDeclaration("1.0", "utf-8", null), // XML declaration
             rssRoot
         );
    }

    private XElement CreateItemElement(PostData post, string baseUrl, SiteContext siteContext) {
        if (string.IsNullOrWhiteSpace(post.Url)) {
            // Should not happen if processing is correct, but handle defensively
            _logger.LogWarning("Skipping RSS item generation for post with empty URL: {SourcePath}", post.SourcePath);
            return new XElement("item", new XComment($"Skipped post with empty URL: {post.SourcePath}"));
        }

        string absoluteUrl = baseUrl + post.Url;

        // Use the full HTML content wrapped in CDATA for the description
        // Alternatively, use post.Summary if available and preferred
        string descriptionContent = post.HtmlContent;
        // if (!string.IsNullOrEmpty(post.Summary)) {
        //     descriptionContent = post.Summary; // Optionally prefer summary
        // }


        var itemElement = new XElement("item",
            new XElement("title", post.FrontMatter.Title ?? "Untitled Post"),
            new XElement("link", absoluteUrl),
            // Use XCData to wrap HTML content correctly
            new XElement("description", new XCData(descriptionContent)),
            // Use RFC 1123 format ("R")
            new XElement("pubDate", post.Date.ToString("R")),
            new XElement("guid", new XAttribute("isPermaLink", "true"), absoluteUrl)
        );

        // Add Categories/Tags
        var categories = (post.FrontMatter.Categories ?? Enumerable.Empty<string>())
                        .Concat(post.FrontMatter.Tags ?? Enumerable.Empty<string>())
                        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var category in categories) {
            itemElement.Add(new XElement("category", category));
        }

        // Add Author
        string authorEmail = post.FrontMatter.Get<string>("author");
        if (!string.IsNullOrWhiteSpace(authorEmail)) {
            // RSS <author> typically expects an email address format
            itemElement.Add(new XElement("author", authorEmail));
        }

        // Optional: Add content:encoded for full content if description is summary
        // Needs contentNs declared on root/channel
        // itemElement.Add(new XElement(contentNs + "encoded", new XCData(post.HtmlContent)));

        return itemElement;
    }
}
