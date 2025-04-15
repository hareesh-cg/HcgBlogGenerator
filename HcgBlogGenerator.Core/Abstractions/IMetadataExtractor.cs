namespace HcgBlogGenerator.Core.Abstractions;

/// <summary>
/// Extracts metadata or generates derived content (like summaries) from content items.
/// </summary>
public interface IMetadataExtractor {
    /// <summary>
    /// Generates a summary/excerpt from HTML content.
    /// Returns null if a summary cannot be generated or is not needed.
    /// </summary>
    /// <param name="htmlContent">The full HTML content of the item.</param>
    /// <param name="maxLength">Approximate maximum length of the summary (implementation defined).</param>
    /// <returns>A plain text summary, or null.</returns>
    string? GenerateSummary(string htmlContent, int maxLength = 250); // Increased default length
}
