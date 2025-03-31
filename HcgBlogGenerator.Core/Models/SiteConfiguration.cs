using Newtonsoft.Json;

namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Represents the overall configuration for the static site generation process,
/// typically loaded from a _config.json file.
/// </summary>
public class SiteConfiguration
{
    /// <summary>
    /// The base URL of the site (e.g., "https://www.example.com").
    /// Used for generating absolute URLs in feeds, sitemaps, etc.
    /// </summary>
    [JsonProperty("baseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The title of the site.
    /// </summary>
    [JsonProperty("title")]
    public string Title { get; set; } = "My Awesome Blog";

    /// <summary>
    /// A short description of the site, often used in meta tags.
    /// </summary>
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>
    /// The default language of the site (e.g., "en-US").
    /// </summary>
    [JsonProperty("language")]
    public string Language { get; set; } = "en-US";

    /// <summary>
    /// The directory where the generated site will be written, relative to the execution root.
    /// Defaults to "_site".
    /// </summary>
    [JsonProperty("outputDirectory")]
    public string OutputDirectory { get; set; } = "_site";

    /// <summary>
    /// The directory containing source files (markdown, assets, etc.), relative to the execution root.
    /// Defaults to the current directory ".".
    /// </summary>
    [JsonProperty("sourceDirectory")]
    public string SourceDirectory { get; set; } = ".";

    /// <summary>
    /// The directory containing layout files, relative to the source directory.
    /// Defaults to "_layouts".
    /// </summary>
    [JsonProperty("layoutsDirectory")]
    public string LayoutsDirectory { get; set; } = "_layouts";

    /// <summary>
    /// The directory containing include files (partials), relative to the source directory.
    /// Defaults to "_includes".
    /// </summary>
    [JsonProperty("includesDirectory")]
    public string IncludesDirectory { get; set; } = "_includes";

    /// <summary>
    /// The directory containing posts, relative to the source directory.
    /// Defaults to "_posts".
    /// </summary>
    [JsonProperty("postsDirectory")]
    public string PostsDirectory { get; set; } = "_posts";

    /// <summary>
    /// The directory containing static assets (CSS, JS, images), relative to the source directory.
    /// Defaults to "_assets".
    /// </summary>
    [JsonProperty("assetsDirectory")]
    public string AssetsDirectory { get; set; } = "_assets";

    /// <summary>
    /// The directory containing draft posts, relative to the source directory.
    /// Defaults to "_drafts".
    /// </summary>
    [JsonProperty("draftsDirectory")]
    public string DraftsDirectory { get; set; } = "_drafts";

    /// <summary>
    /// Indicates whether to include draft posts in the build. Defaults to false.
    /// Can be overridden by command-line arguments.
    /// </summary>
    [JsonProperty("includeDrafts")]
    public bool IncludeDrafts { get; set; } = false;

    /// <summary>
    /// The default number of posts per page for pagination.
    /// Null means pagination is disabled by default.
    /// </summary>
    [JsonProperty("postsPerPage")]
    public int? PostsPerPage { get; set; }

    /// <summary>
    /// A dictionary for storing custom data that can be accessed in templates.
    /// </summary>
    [JsonProperty("data")]
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Configuration settings specific to plugins.
    /// The key is the plugin name (case-insensitive), and the value is the plugin-specific configuration object.
    /// </summary>
    [JsonProperty("plugins")]
    public Dictionary<string, object> PluginSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // TODO: Add more configuration options as needed (e.g., author info, social links, build options)
} 