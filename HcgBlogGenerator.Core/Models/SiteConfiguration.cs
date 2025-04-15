namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Represents the site-wide configuration loaded from the config file (e.g., config.json).
/// </summary>
public class SiteConfiguration {
    /// <summary>
    /// The base URL of the deployed site (e.g., "https://www.example.com").
    /// Used for generating absolute URLs in feeds, sitemaps, etc.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The main title of the website.
    /// </summary>
    public string Title { get; set; } = "My Awesome Blog";

    /// <summary>
    /// A short description or tagline for the website.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Default language code for the site (e.g., "en-US").
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Number of posts to display per page on listing pages.
    /// </summary>
    public int PostsPerPage { get; set; } = 10;

    /// <summary>
    /// The directory containing content files (posts, pages), relative to the source root.
    /// </summary>
    public string ContentDirectory { get; set; } = "content";

    /// <summary>
    /// The directory containing template files (layouts, includes), relative to the source root.
    /// </summary>
    public string TemplateDirectory { get; set; } = "layouts"; // Or maybe includes layouts/, includes/?

    /// <summary>
    /// The directory for includes/partials, relative to the TemplateDirectory.
    /// </summary>
    public string IncludesDirectory { get; set; } = "includes";

    /// <summary>
    /// The directory containing static files to be copied directly, relative to the source root.
    /// </summary>
    public string StaticDirectory { get; set; } = "static";

    /// <summary>
    /// The directory containing SCSS/SASS files, relative to the source root.
    /// </summary>
    public string StylesDirectory { get; set; } = "styles";

    /// <summary>
    /// The main SCSS/SASS file to compile, relative to the StylesDirectory.
    /// </summary>
    public string StyleEntryPoint { get; set; } = "main.scss";

    /// <summary>
    /// The directory where the generated site will be written, relative to the execution path or a specified output path.
    /// </summary>
    public string OutputDirectory { get; set; } = "_site"; // Common default like Jekyll

    /// <summary>
    /// Permalink structure for posts. Placeholders like :year, :month, :day, :title, :slug are common.
    /// Example: "/blog/:year/:month/:slug/"
    /// </summary>
    public string PostPermalink { get; set; } = "/posts/:slug/";

    /// <summary>
    /// Permalink structure for pages. Placeholder :slug is common.
    /// Example: "/:slug/"
    /// </summary>
    public string PagePermalink { get; set; } = "/:slug/";

    /// <summary>
    /// Controls whether draft posts are built.
    /// </summary>
    public bool BuildDrafts { get; set; } = false;

    /// <summary>
    /// Controls whether future-dated posts are built.
    /// </summary>
    public bool BuildFutureDated { get; set; } = false;

    /// <summary>
    /// Base path for generated tag pages (e.g., "/tags/").
    /// </summary>
    public string TagUrlBasePath { get; set; } = "/tags/";

    /// <summary>
    /// Base path for generated category pages (e.g., "/categories/").
    /// </summary>
    public string CategoryUrlBasePath { get; set; } = "/categories/";

    /// <summary>
    /// Configuration specific to the RSS feed generation.
    /// </summary>
    public RssFeedConfiguration Rss { get; set; } = new RssFeedConfiguration();

    /// <summary>
    /// Allows for arbitrary additional configuration data.
    /// </summary>
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Configuration specific to RSS feed generation.
/// </summary>
public class RssFeedConfiguration {
    /// <summary>
    /// Enable or disable RSS feed generation.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Output path for the RSS feed file relative to the output directory.
    /// </summary>
    public string OutputPath { get; set; } = "feed.xml";

    /// <summary>
    /// Maximum number of posts to include in the feed.
    /// </summary>
    public int MaxItems { get; set; } = 20;
}
