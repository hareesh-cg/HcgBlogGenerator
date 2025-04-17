using System.Text;
using System.Xml.Linq;

using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Models;

using Microsoft.Extensions.Logging;

namespace HcgBlogGenerator.Core.Plugins;

/// <summary>
/// Generates a sitemap.xml file based on the processed site content.
/// </summary>
public class SitemapPlugin : IPlugin {
    private readonly ILogger<SitemapPlugin> _logger;
    public string Name => "Sitemap Generator";

    // XML Namespace for sitemap protocol
    private static readonly XNamespace Xmlns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public SitemapPlugin(ILogger<SitemapPlugin> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(
        PipelineStage stage,
        SiteContext siteContext,
        IFileSystem sourceFileSystem, // Not used directly here, but part of the signature
        IFileSystem outputFileSystem,
        CancellationToken cancellationToken = default) {
        if (stage != PipelineStage.PostBuild) {
            return; // Only run after the main site files are built
        }

        _logger.LogInformation("Executing {PluginName}...", Name);
        string outputPath = "sitemap.xml"; // Relative to output root

        // Basic check: BaseUrl is required for a valid sitemap
        if (string.IsNullOrWhiteSpace(siteContext.Configuration.BaseUrl)) {
            _logger.LogWarning("{PluginName}: BaseUrl is not configured. Cannot generate absolute URLs for sitemap. Skipping.", Name);
            return;
        }

        try {
            var sitemapEntries = GenerateSitemapEntries(siteContext);

            if (!sitemapEntries.Any()) {
                _logger.LogWarning("{PluginName}: No eligible content found to include in the sitemap.", Name);
                return;
            }

            XDocument sitemapDocument = CreateSitemapDocument(sitemapEntries);

            // Use XmlWriter for controlled output without BOM (Byte Order Mark) if needed,
            // or simply use XDocument.ToString() / XDocument.Save() with appropriate encoding.
            // Let's use Save with options for clarity.

            // Ensure output directory exists (WriteAllTextAsync usually handles this, but good practice)
            // await outputFileSystem.CreateDirectoryAsync(Path.GetDirectoryName(outputPath), cancellationToken); // Not needed if outputPath is root

            using (var memoryStream = new MemoryStream())
            // Use an intermediate MemoryStream to control encoding accurately
            {
                // Save to stream with UTF-8 encoding WITHOUT BOM
                // XmlWriter is more explicit about this
                var writerSettings = new System.Xml.XmlWriterSettings {
                    Async = true,
                    Encoding = new UTF8Encoding(false), // UTF-8 without BOM
                    Indent = true // Make output readable
                };

                using (var writer = System.Xml.XmlWriter.Create(memoryStream, writerSettings)) {
                    await sitemapDocument.WriteToAsync(writer, cancellationToken);
                } // Writer is flushed and disposed here

                memoryStream.Position = 0; // Reset stream position

                _logger.LogDebug("Writing sitemap.xml content to {OutputPath}", outputPath);
                await outputFileSystem.WriteStreamAsync(outputPath, memoryStream, cancellationToken);
            }


            _logger.LogInformation("{PluginName} finished successfully. Generated sitemap with {EntryCount} entries.", Name, sitemapEntries.Count());
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error generating or writing sitemap.xml file to {OutputPath}", outputPath);
            // Don't re-throw usually for non-critical post-build steps
        }
    }

    private IEnumerable<SitemapEntry> GenerateSitemapEntries(SiteContext siteContext) {
        string baseUrl = siteContext.Configuration.BaseUrl.TrimEnd('/');

        // Combine posts and pages for sitemap generation
        var allContent = siteContext.Posts
            .Cast<ContentItem>() // Cast PostData to base ContentItem
            .Concat(siteContext.Pages.Cast<ContentItem>()); // Concatenate with PageData

        foreach (var item in allContent) {
            if (item.FrontMatter.Draft) {
                _logger.LogTrace("Skipping draft item {SourcePath} from sitemap.", item.SourcePath);
                continue;
            }
            // Skip if URL is missing or invalid (shouldn't happen with proper processing)
            if (string.IsNullOrWhiteSpace(item.Url)) continue;

            // Construct absolute URL
            string absoluteUrl = baseUrl + item.Url;

            // Determine last modified date
            // Use frontmatter.LastModified > frontmatter.Date > File Write Time (if available)?
            // For simplicity, use frontmatter Date primarily for posts, maybe default for pages
            DateTimeOffset lastMod = item.FrontMatter.LastModified ?? (item as PostData)?.Date ?? DateTimeOffset.UtcNow; // Fallback to now

            // Determine change frequency (can be estimated or set in frontmatter)
            // Example simple logic: Posts change less frequently than index pages?
            string changeFreq = (item is PostData) ? "monthly" : "weekly"; // Default estimate

            // Determine priority (can be set in frontmatter)
            // Example simple logic: Homepage high, posts medium, pages low?
            double priority = (item.Url == "/") ? 1.0 : (item is PostData) ? 0.8 : 0.5; // Default estimate

            // Allow overrides from FrontMatter
            changeFreq = item.FrontMatter.Get<string>("sitemapChangeFreq") ?? changeFreq;
            priority = item.FrontMatter.Get<double?>("sitemapPriority") ?? priority;

            // Validate priority range
            priority = Math.Clamp(priority, 0.0, 1.0);


            yield return new SitemapEntry(
                Loc: absoluteUrl,
                LastMod: lastMod,
                ChangeFreq: changeFreq,
                Priority: priority
            );
        }
        // TODO: Add entries for Taxonomy pages and Paginated list pages if generated
    }

    private XDocument CreateSitemapDocument(IEnumerable<SitemapEntry> entries) {
        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(Xmlns + "urlset", // Root element <urlset> with namespace
                entries.Select(entry =>
                    new XElement(Xmlns + "url",
                        new XElement(Xmlns + "loc", entry.Loc),
                        new XElement(Xmlns + "lastmod", entry.LastMod.ToString("yyyy-MM-ddTHH:mm:sszzz")), // W3C format
                        new XElement(Xmlns + "changefreq", entry.ChangeFreq),
                        new XElement(Xmlns + "priority", entry.Priority.ToString("F1")) // Format priority to one decimal place
                    )
                )
            )
        );
    }

    // Simple record to hold sitemap entry data
    private record SitemapEntry(string Loc, DateTimeOffset LastMod, string ChangeFreq, double Priority);
}
