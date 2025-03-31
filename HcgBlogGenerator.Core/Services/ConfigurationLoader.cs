using HcgBlogGenerator.Core.Interfaces;
using HcgBlogGenerator.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Loads site configuration from a JSON file using the provided IFileSystem.
/// </summary>
public class ConfigurationLoader : IConfigurationLoader
{
    private readonly ILogger<ConfigurationLoader> _logger;

    public ConfigurationLoader(ILogger<ConfigurationLoader> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SiteConfiguration?> LoadConfigurationAsync(string configFilePath, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to load configuration from: {ConfigFilePath}", configFilePath);

        if (!await fileSystem.FileExistsAsync(configFilePath))
        {
            _logger.LogWarning("Configuration file not found at: {ConfigFilePath}. Using default configuration.", configFilePath);
            // Return a default configuration object instead of null
            // This makes downstream code simpler as it doesn't constantly need to null-check the config
            return new SiteConfiguration();
        }

        try
        {
            var jsonContent = await fileSystem.ReadAllTextAsync(configFilePath, cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogWarning("Configuration file is empty: {ConfigFilePath}. Using default configuration.", configFilePath);
                return new SiteConfiguration();
            }

            var configuration = JsonConvert.DeserializeObject<SiteConfiguration>(jsonContent);

            if (configuration == null)
            {
                _logger.LogError("Failed to deserialize configuration file: {ConfigFilePath}. Content might be invalid JSON. Using default configuration.", configFilePath);
                return new SiteConfiguration(); // Return default if deserialization results in null
            }

            _logger.LogInformation("Successfully loaded configuration from: {ConfigFilePath}", configFilePath);
            // TODO: Add validation logic for configuration values (e.g., ensure BaseUrl is valid URI if set)
            return configuration;
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "Error parsing configuration file JSON: {ConfigFilePath}. Using default configuration.", configFilePath);
            return new SiteConfiguration(); // Return default on JSON parsing error
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading configuration file: {ConfigFilePath}. Using default configuration.", configFilePath);
            return new SiteConfiguration(); // Return default on other errors
        }
    }
} 