using YamlDotNet.Serialization;

namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Represents the metadata extracted from the YAML front matter of a content file (e.g., Markdown post or page).
/// </summary>
public class FrontMatter
{
    /// <summary>
    /// The title of the content item.
    /// </summary>
    [YamlMember(Alias = "title")]
    public string? Title { get; set; }

    /// <summary>
    /// The publication date of the content item. Required for posts.
    /// </summary>
    [YamlMember(Alias = "date")]
    public DateTime? Date { get; set; }

    /// <summary>
    /// The layout file to use for rendering this content item.
    /// If null, a default layout might be used or rendering might differ.
    /// </summary>
    [YamlMember(Alias = "layout")]
    public string? Layout { get; set; }

    /// <summary>
    /// Indicates whether the content item is published. Defaults to true.
    /// Items marked as false might be treated as drafts.
    /// </summary>
    [YamlMember(Alias = "published")]
    public bool Published { get; set; } = true;

    /// <summary>
    /// A list of tags associated with the content item.
    /// </summary>
    [YamlMember(Alias = "tags")]
    public List<string>? Tags { get; set; }

    /// <summary>
    /// A list of categories associated with the content item.
    /// </summary>
    [YamlMember(Alias = "categories")]
    public List<string>? Categories { get; set; }

    /// <summary>
    /// The URL slug for the content item. If not specified, it might be generated from the filename or title.
    /// </summary>
    [YamlMember(Alias = "permalink")]
    public string? Permalink { get; set; }

    /// <summary>
    /// A short excerpt or summary of the content. If not specified, it might be auto-generated.
    /// </summary>
    [YamlMember(Alias = "excerpt")]
    public string? Excerpt { get; set; }

    /// <summary>
    /// Catches any additional custom properties defined in the front matter.
    /// Allows for flexibility in defining custom metadata.
    /// </summary>
    [YamlIgnore] // Ignored by YamlDotNet during direct deserialization to this type
    public Dictionary<string, object> CustomProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Note: YamlDotNet can deserialize extra members into a dictionary if configured,
    // but defining CustomProperties explicitly makes the model clearer.
    // We will handle populating this dictionary in the IFrontMatterParser implementation.
} 