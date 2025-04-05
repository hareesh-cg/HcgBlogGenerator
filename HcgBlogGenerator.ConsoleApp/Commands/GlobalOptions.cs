using System.CommandLine;
using System.IO; // For Path

namespace HcgBlogGenerator.ConsoleApp.Commands;

/// <summary>
/// Defines global options applicable to multiple commands.
/// </summary>
public static class GlobalOptions {
    public static readonly Option<DirectoryInfo> SourceOption =
        new Option<DirectoryInfo>(
            aliases: new[] { "--source", "-s" },
            // Use lambda for default value based on current directory
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            description: "Path to the source directory of the blog/site.") {
            // Ensure the directory exists for commands that need it? Or let commands validate.
            // ArgumentHelpName = "source_dir" // Optional: Customize help display
        };

    public static readonly Option<DirectoryInfo> OutputOption =
        new Option<DirectoryInfo>(
            aliases: new[] { "--output", "-o" },
            // Default output is _site relative to source (calculated later)
            description: "Path to the output directory for the generated site. Defaults to '_site' relative to source.") {
            // ArgumentHelpName = "output_dir"
        };

    public static readonly Option<FileInfo> ConfigOption =
        new Option<FileInfo>(
            aliases: new[] { "--config", "-c" },
            // Default config is config.json relative to source (calculated later)
            description: "Path to the site configuration file. Defaults to 'config.json' relative to source.") {
            // ArgumentHelpName = "config_file"
        };
}
