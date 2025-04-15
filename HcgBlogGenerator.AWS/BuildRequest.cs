using System.Text.Json.Serialization;

namespace HcgBlogGenerator.Aws;

/// <summary>
/// Represents the expected JSON body for triggering a build via POST request.
/// </summary>
public class BuildRequest {
    // Use JsonPropertyName to match expected JSON keys if they differ from C# conventions
    [JsonPropertyName("source_bucket")]
    public string? SourceBucket {
        get; set;
    }

    [JsonPropertyName("source_prefix")]
    public string? SourcePrefix {
        get; set;
    } // Optional

    [JsonPropertyName("output_bucket")]
    public string? OutputBucket {
        get; set;
    }

    [JsonPropertyName("output_prefix")]
    public string? OutputPrefix {
        get; set;
    } // Optional

    [JsonPropertyName("config_file_key")]
    public string? ConfigFileKey {
        get; set;
    } // Optional
}
