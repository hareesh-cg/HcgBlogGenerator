using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Plugins;
using HcgBlogGenerator.Core.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HcgBlogGenerator.Core.Utilities;

public static class ServiceCollectionExtensions {
    /// <summary>
    /// Registers the core services, implementations, and plugins for HcgBlogGenerator.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="sourceDirectory">The absolute path to the source directory of the site.</param>
    /// <param name="outputDirectory">The absolute path to the output directory for the site.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHcgBlogGeneratorCore(this IServiceCollection services) {
        // --- Register Core Services ---

        // Register FileSystems (using factory to provide root path)
        // We register IFileSystem twice with specific instances named for source/output if needed,
        // but usually SiteBuilder expects specific instances passed to BuildAsync.
        // Let's register the *implementations* so they can be created.
        // The caller (e.g., CLI) will create instances with specific roots.
        // Alternatively, provide factory methods if DI should manage instances directly.
        // For now, let's just register the types needed.

        // Register Implementations (Transient or Scoped depending on state)
        services.AddSingleton<IMetadataExtractor, MetadataExtractor>();
        services.AddSingleton<MarkdigContentParser>();
        services.AddSingleton<HtmlContentParser>();
        services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>(); // Template engine can often be singleton if thread-safe and state loaded once
        services.AddSingleton<ICssCompiler, LibSassCompiler>();
        services.AddSingleton<ISiteBuilder, SiteBuilder>(); // SiteBuilder orchestrates a single build

        // Register Plugin Management
        services.AddSingleton<PluginManager>(); // PluginManager manages registered plugins

        // --- Register Concrete Plugins ---
        // Register all classes implementing IPlugin. Use Scrutor for assembly scanning later if needed.
        services.AddSingleton<IPlugin, ReadingTimePlugin>();
        services.AddSingleton<IPlugin, RobotsPlugin>();
        services.AddSingleton<IPlugin, SitemapPlugin>();
        services.AddSingleton<IPlugin, RssPlugin>();
        services.AddSingleton<IPlugin, SeoPlugin>();

        // Note on FileSystem registration:
        // Usually, the application entry point (CLI, Lambda) will create IFileSystem instances
        // with the correct root paths (source/output) and pass them to ISiteBuilder.BuildAsync.
        // If SiteBuilder needed to *resolve* specific IFileSystem instances via DI,
        // you'd need named registrations or a factory pattern. Keeping it simple for now.

        return services;
    }
}
