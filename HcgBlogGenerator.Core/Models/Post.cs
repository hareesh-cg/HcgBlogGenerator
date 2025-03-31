namespace HcgBlogGenerator.Core.Models;

/// <summary>
/// Represents a blog post within the site.
/// Inherits common properties from ContentItem and adds post-specific logic.
/// </summary>
public class Post : ContentItem
{
    /// <summary>
    /// The publication date of the post. For posts, this is generally expected to be non-null.
    /// It overrides the base implementation to potentially enforce or default the date later if needed,
    /// but currently relies on the FrontMatter date.
    /// </summary>
    public override DateTime? Date => base.Date; // Could add logic here later if needed

    /// <summary>
    /// The title of the post. Overrides the base implementation to provide a fallback
    /// if the title is missing in the front matter (e.g., generate from filename).
    /// </summary>
    public override string? Title
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(base.Title))
            {
                return base.Title;
            }

            // Fallback: Generate a title from the source filename if no title is provided
            // Example: "YYYY-MM-DD-my-first-post.md" -> "My First Post"
            // This logic might need refinement based on actual filename conventions.
            var fileName = System.IO.Path.GetFileNameWithoutExtension(SourcePath);
            // Simple heuristic: remove date prefix if present (YYYY-MM-DD-)
            if (fileName.Length > 11 && fileName[4] == '-' && fileName[7] == '-')
            {
                try
                {
                    if (DateTime.TryParse(fileName.Substring(0, 10), out _))
                    {
                        fileName = fileName.Substring(11);
                    }
                } catch { /* Ignore parsing errors */ }
            }
            // Replace hyphens with spaces and capitalize words
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(fileName.Replace('-', ' '));
        }
    }

    // Potential future properties specific to posts:
    // public Author Author { get; set; }
    // public ReadingTime ReadingTime { get; set; }
    // public List<Comment> Comments { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Post"/> class.
    /// </summary>
    public Post() : base() { }
} 