using HcgBlogGenerator.Core.Interfaces;
using HcgBlogGenerator.Core.Models;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Parses YAML front matter from text content using YamlDotNet.
/// </summary>
public class FrontMatterParser : IFrontMatterParser
{
    private const string Separator = "---";
    private readonly ILogger<FrontMatterParser> _logger;
    private readonly IDeserializer _yamlDeserializer;

    public FrontMatterParser(ILogger<FrontMatterParser> logger)
    {
        _logger = logger;
        // Configure YamlDotNet - ignore extra properties during direct deserialization
        // and use camelCase naming convention for flexibility, although we primarily use explicit mapping.
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance) // Handles common variations like 'baseUrl'
            .IgnoreUnmatchedProperties() // Important: Prevents errors if YAML has fields not in FrontMatter class
            .Build();
    }

    /// <inheritdoc />
    public (FrontMatter FrontMatter, string Content) Parse(string content)
    {
        var frontMatter = new FrontMatter(); // Start with a default object

        if (string.IsNullOrWhiteSpace(content))
        {
            return (frontMatter, string.Empty);
        }

        // Check for the starting separator
        if (!content.StartsWith(Separator + Environment.NewLine) && !content.StartsWith(Separator + "\n"))
        {
            _logger.LogDebug("No front matter separator found at the beginning of the content.");
            return (frontMatter, content); // No front matter found
        }

        // Find the end separator
        var startOfContentIndex = content.IndexOf(Separator + Environment.NewLine, Separator.Length);
        if (startOfContentIndex < 0)
        {
            startOfContentIndex = content.IndexOf(Separator + "\n", Separator.Length);
        }


        if (startOfContentIndex < 0)
        {
            _logger.LogWarning("Starting front matter separator '---' found, but no ending separator.");
            // Treat as if no valid front matter exists
            return (frontMatter, content);
        }

        // Extract the YAML block
        var yamlBlock = content.Substring(Separator.Length, startOfContentIndex - Separator.Length).Trim();
        // Extract the content after the front matter
        var remainingContent = content.Substring(startOfContentIndex + Separator.Length).TrimStart('\r', '\n');

        if (string.IsNullOrWhiteSpace(yamlBlock))
        {
            _logger.LogDebug("Front matter block is empty.");
            return (frontMatter, remainingContent); // Empty front matter
        }

        try
        {
            // First, deserialize into the strongly-typed object to get known properties
            frontMatter = _yamlDeserializer.Deserialize<FrontMatter>(yamlBlock) ?? new FrontMatter();

            // Second, deserialize into a dictionary to capture *all* properties, including custom ones
            var allProperties = _yamlDeserializer.Deserialize<Dictionary<string, object>>(yamlBlock);

            if (allProperties != null)
            {
                // Get the names of properties defined in the FrontMatter class
                var knownPropertyNames = typeof(FrontMatter).GetProperties()
                    .Select(p => p.GetCustomAttributes(typeof(YamlMemberAttribute), true)
                                  .OfType<YamlMemberAttribute>()
                                  .FirstOrDefault()?.Alias ?? p.Name.ToLowerInvariant())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Populate CustomProperties with entries not explicitly defined in FrontMatter
                foreach (var kvp in allProperties)
                {
                    // Use the original YAML key for the CustomProperties dictionary
                    if (!knownPropertyNames.Contains(kvp.Key.ToLowerInvariant()))
                    {
                        frontMatter.CustomProperties[kvp.Key] = kvp.Value;
                    }
                }
            }

             _logger.LogDebug("Successfully parsed front matter.");
        }
        catch (YamlException ex)
        {
            _logger.LogError(ex, "Error parsing YAML front matter. Content will be treated as having no front matter.");
            // Reset to default front matter and return original content as if no separator was found initially
            // This prevents processing potentially broken content.
            return (new FrontMatter(), content);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Unexpected error during front matter parsing.");
             return (new FrontMatter(), content); // Return default on other errors
        }

        return (frontMatter, remainingContent);
    }
} 
