namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Holds the overall configuration and processed content for the site during generation.
/// This object is often passed around during the build process.
/// </summary>
public class SiteContext {
    /// <summary>
    /// Site-wide configuration loaded from the config file.
    /// </summary>
    public SiteConfiguration Configuration { get; set; }

    /// <summary>
    /// List of all processed blog posts, typically sorted by date descending.
    /// </summary>
    public List<PostData> Posts { get; set; } // Settable for easier manipulation during build

    /// <summary>
    /// List of all processed standalone pages.
    /// </summary>
    public List<PageData> Pages { get; set; } // Settable

    /// <summary>
    /// List of all other content items (e.g., drafts if loaded, custom collections).
    /// </summary>
    public List<ContentItem> OtherContent { get; set; } // Settable

    /// <summary>
    /// Processed taxonomy data (e.g., Categories, Tags).
    /// Dictionary key is taxonomy type ("category", "tag"), value is another dictionary of term -> posts.
    /// </summary>
    public Dictionary<string, Dictionary<string, List<PostData>>> Taxonomies { get; set; } // Settable

    /// <summary>
    /// List of dynamically generated list pages (e.g., tag/category archives).
    /// Populated during the build process after taxonomies are processed.
    /// </summary>
    public List<ListPageData> ListPages { get; set; } // Settable

    // Constructor requiring the essential configuration
    public SiteContext(SiteConfiguration configuration) {
        this.Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Initialize collections
        this.Posts = new List<PostData>();
        this.Pages = new List<PageData>();
        this.OtherContent = new List<ContentItem>();
        this.Taxonomies = new Dictionary<string, Dictionary<string, List<PostData>>>();
        this.ListPages = new List<ListPageData>();
    }
}
