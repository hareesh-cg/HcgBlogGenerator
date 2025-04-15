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
        services.AddTransient<IMetadataExtractor, MetadataExtractor>();
        services.AddTransient<IContentParser, MarkdigContentParser>();
        services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>(); // Template engine can often be singleton if thread-safe and state loaded once
        services.AddTransient<ICssCompiler, LibSassCompiler>();
        services.AddTransient<ISiteBuilder, SiteBuilder>(); // SiteBuilder orchestrates a single build

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

    /// <summary>
    /// Registers a specific IFileSystem implementation (LocalFileSystem) for a given root path.
    /// This is an alternative approach if DI needs to provide configured instances.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="rootPath">The absolute root path for this filesystem instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalFileSystem(this IServiceCollection services, string rootPath, bool forceClean) {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentNullException(nameof(rootPath));

        // Ensure directory exists before registering? Or let constructor handle it?
        // Let constructor handle it for now.

        // Register factory method
        services.AddTransient<LocalFileSystem>(sp =>
            new LocalFileSystem(rootPath, forceClean, sp.GetRequiredService<ILogger<LocalFileSystem>>())
        );

        // Optionally, register it as IFileSystem IF ONLY ONE is needed directly via DI
        // services.AddTransient<IFileSystem>(sp => sp.GetRequiredService<LocalFileSystem>());

        return services;
    }
}
