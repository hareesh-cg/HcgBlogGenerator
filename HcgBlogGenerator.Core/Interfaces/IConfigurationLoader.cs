using HcgBlogGenerator.Core.Models;

namespace HcgBlogGenerator.Core.Interfaces;

/// <summary>
/// Defines the contract for loading site configuration.
/// </summary>
public interface IConfigurationLoader
{
    /// <summary>
    /// Asynchronously loads the site configuration from the specified path.
    /// </summary>
    /// <param name="configFilePath">The path to the configuration file (e.g., "_config.json").</param>
    /// <param name="fileSystem">The file system abstraction to use for reading the file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded SiteConfiguration, or null if the file doesn't exist or cannot be parsed.</returns>
    Task<SiteConfiguration?> LoadConfigurationAsync(string configFilePath, IFileSystem fileSystem, CancellationToken cancellationToken = default);
} 