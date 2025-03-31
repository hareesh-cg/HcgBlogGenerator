namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Represents a standard page within the site (e.g., About, Contact).
/// Inherits common properties from ContentItem.
/// </summary>
public class Page : ContentItem
{
    // Currently, no page-specific properties are needed beyond what ContentItem provides.
    // This class exists to differentiate pages from posts and other potential content types.

    /// <summary>
    /// Initializes a new instance of the <see cref="Page"/> class.
    /// </summary>
    public Page() : base() { }

    // Example of potentially overriding a property if needed later:
    // public override string? Title => base.Title ?? "Untitled Page";
} 