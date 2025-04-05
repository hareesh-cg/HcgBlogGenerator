using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Models;

using Microsoft.Extensions.Logging;

using System;
using System.Text; // For StringBuilder
using System.Threading;
using System.Threading.Tasks;

namespace HcgBlogGenerator.Core.Plugins;

/// <summary>
/// Generates a robots.txt file in the output directory.
/// </summary>
public class RobotsPlugin : IPlugin {
    private readonly ILogger<RobotsPlugin> _logger;
    public string Name => "Robots.txt Generator";

    public RobotsPlugin(ILogger<RobotsPlugin> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(
        PipelineStage stage,
        SiteContext siteContext,
        IFileSystem sourceFileSystem,
        IFileSystem outputFileSystem,
        CancellationToken cancellationToken = default) {
        // This plugin runs after the main build is complete
        if (stage != PipelineStage.PostBuild) {
            return;
        }

        _logger.LogInformation("Executing {PluginName}...", Name);
        string outputPath = "robots.txt"; // Output path relative to output root

        try {
            // --- Generate robots.txt Content ---
            // This is a very basic example. Could be driven by configuration.
            var contentBuilder = new StringBuilder();
            contentBuilder.AppendLine("User-agent: *"); // Apply rules to all user agents
            contentBuilder.AppendLine("Allow: /");    // Allow crawling everything by default

            // Optional: Add disallowed paths if needed (e.g., from config)
            // if (siteContext.Configuration.DisallowedPaths != null) {
            //    foreach(var path in siteContext.Configuration.DisallowedPaths) {
            //        contentBuilder.AppendLine($"Disallow: {path}");
            //    }
            // }

            // Optional: Add Sitemap location
            // TODO: Get sitemap path from config or another plugin's output
            string sitemapUrl = siteContext.Configuration.BaseUrl?.TrimEnd('/') + "/sitemap.xml";
            if (!string.IsNullOrWhiteSpace(siteContext.Configuration.BaseUrl)) {
                contentBuilder.AppendLine($"Sitemap: {sitemapUrl}");
            }


            string robotsContent = contentBuilder.ToString();

            // --- Write to Output ---
            _logger.LogDebug("Writing robots.txt content to {OutputPath}", outputPath);
            await outputFileSystem.WriteAllTextAsync(outputPath, robotsContent, cancellationToken);

            _logger.LogInformation("{PluginName} finished successfully.", Name);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error generating or writing robots.txt file to {OutputPath}", outputPath);
            // Don't re-throw typically for non-critical post-build steps
        }
    }
}
