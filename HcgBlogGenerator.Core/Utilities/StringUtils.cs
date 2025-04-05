using System.Text.RegularExpressions;

namespace HcgBlogGenerator.Core.Utilities;

public static class StringUtils {
    // Basic Slugify function (copied/moved from SiteBuilder)
    public static string Slugify(string? text) // Make parameter nullable for safety
    {
        if (string.IsNullOrWhiteSpace(text)) return "untitled";

        string output = text.ToLowerInvariant();
        // Replace invalid characters (allow letters, numbers, hyphen)
        output = Regex.Replace(output, @"[^a-z0-9\s-]", "");
        // Replace spaces with single hyphen
        output = Regex.Replace(output, @"\s+", "-");
        // Replace multiple consecutive hyphens with single hyphen
        output = Regex.Replace(output, @"-{2,}", "-");
        // Trim leading/trailing hyphens
        output = output.Trim('-');

        // Handle cases where input becomes empty after cleaning
        if (string.IsNullOrWhiteSpace(output)) return "untitled";

        return output;
    }
}
