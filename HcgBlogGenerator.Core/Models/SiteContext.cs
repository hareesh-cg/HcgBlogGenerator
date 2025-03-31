namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Represents the site-wide data accessible within templates, including configuration and collections.
/// </summary>
public class SiteContext {
    // Note: Accessing lists of posts, pages, etc., will typically be done via the SiteContext.
    // Example: `site.posts`, `site.pages`

    /// <summary>
    /// The base URL of the site.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// The title of the site.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// A short description of the site.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The default language of the site.
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// All posts, typically sorted by date descending.
    /// </summary>
    public IReadOnlyList<Post> Posts { get; set; }

    /// <summary>
    /// All pages.
    /// </summary>
    public IReadOnlyList<Page> Pages { get; set; }

    /// <summary>
    /// All content items (posts and pages combined).
    /// </summary>
    public IReadOnlyList<ContentItem> AllContent { get; set; }

    /// <summary>
    /// Custom data from the _config.json `data` field.
    /// </summary>
    public IReadOnlyDictionary<string, object> Data { get; set; }

    /// <summary>
    /// The current time during the build process. Can be used for cache busting, etc.
    /// </summary>
    public DateTime Time { get; } = DateTime.UtcNow;

    // Potential future additions:
    // public ILookup<string, Post> PostsByTag { get; init; }
    // public ILookup<string, Post> PostsByCategory { get; init; }
    // public IReadOnlyList<Page> NavigationPages { get; init; } // Filtered pages for menus

    /// <summary>
    /// Creates a SiteContext from a SiteConfiguration and collections.
    /// </summary>
    public static SiteContext FromConfiguration(SiteConfiguration config, IEnumerable<Post> posts, IEnumerable<Page> pages) {
        var sortedPosts = posts.OrderByDescending(p => p.Date ?? DateTime.MinValue).ToList();
        var allPages = pages.ToList(); // Assuming order isn't critical for pages, or sort if needed
        var allContent = new List<ContentItem>(sortedPosts);
        allContent.AddRange(allPages);

        return new SiteContext {
            BaseUrl = config.BaseUrl,
            Title = config.Title,
            Description = config.Description,
            Language = config.Language,
            Posts = sortedPosts.AsReadOnly(),
            Pages = allPages.AsReadOnly(),
            AllContent = allContent.AsReadOnly(), // Consider sorting if needed
            Data = config.Data // Should be ReadOnlyDictionary, ensure config loader returns this or convert
        };
    }
}
