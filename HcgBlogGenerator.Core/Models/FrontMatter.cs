using System.Text.Json.Serialization; // Keep for JsonExtensionData if using System.Text.Json for config/frontmatter parsing later

namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Represents the metadata extracted from content frontmatter (typically YAML or TOML).
/// Uses Dictionary for flexibility, but specific properties are common.
/// </summary>
public class FrontMatter {
    /// <summary>
    /// Title of the content.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Publication date. Nullable for pages or items without a date.
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// Last modification date. Optional.
    /// </summary>
    public DateTime? LastModified { get; set; }

    /// <summary>
    /// Layout template to use (e.g., "post", "default"). Null implies default layout.
    /// </summary>
    public string? Layout { get; set; }

    /// <summary>
    /// List of categories.
    /// </summary>
    public List<string> Categories { get; set; } = new List<string>();

    /// <summary>
    /// List of tags.
    /// </summary>
    public List<string> Tags { get; set; } = new List<string>();

    /// <summary>
    /// Indicates if the content is a draft. Defaults to false.
    /// </summary>
    public bool Draft { get; set; } = false;

    /// <summary>
    /// Explicitly sets the output URL path relative to the base URL. Overrides permalink generation.
    /// Should start and end with '/'. Example: "/my-custom-page/"
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Custom slug to use in permalink generation instead of deriving from filename.
    /// </summary>
    public string? Slug { get; set; }

    /// <summary>
    /// Summary or excerpt for the content. Can be generated or specified.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Catches any other key-value pairs defined in the frontmatter.
    /// Useful for custom metadata used by templates or plugins.
    /// </summary>
    [JsonExtensionData] // Keep attribute if using System.Text.Json for parsing
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();

    // Helper to get extra data with type safety and potential conversion
    public T? Get<T>(string key) {
        if (ExtraData.TryGetValue(key, out var value)) {
            if (value is T typedValue) {
                return typedValue;
            }
            // Handle potential type mismatches (e.g., if YAML parser reads numbers as long/double)
            try {
                // Special handling for common numeric conversions if needed
                if (typeof(T) == typeof(int) && value is long longValue) return (T)(object)(int)longValue;
                if (typeof(T) == typeof(double) && value is decimal decValue) return (T)(object)(double)decValue;
                // Add other specific conversions if necessary based on parser behavior

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException) {
                // Log warning or handle appropriately
                Console.Error.WriteLine($"Warning: Could not convert frontmatter key '{key}' value '{value}' to type {typeof(T).Name}.");
                return default;
            }
        }
        return default;
    }
}
