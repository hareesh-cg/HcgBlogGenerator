// --- HcgBlogGenerator.Core/Models/Layout.cs ---

namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Represents a layout template file used for rendering content items.
/// </summary>
public class Layout {
    /// <summary>
    /// The name of the layout (usually derived from the filename without extension).
    /// Used in front matter to specify which layout to use.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The absolute path to the layout file.
    /// </summary>
    public string FilePath { get; set; }

    /// <summary>
    /// The raw content of the layout file.
    /// </summary>
    public string Content { get; set; }

    // Consider adding caching for compiled templates if needed at this level,
    // though the ITemplateEngine might handle caching internally.
    // public ICompiledTemplate? CompiledTemplate { get; set; }
}
