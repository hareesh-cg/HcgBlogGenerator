using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Services; // For LocalFileSystem
using HcgBlogGenerator.Core.Utilities; // For SiteConstants

using Microsoft.Extensions.DependencyInjection; // For IServiceProvider
using Microsoft.Extensions.Logging;

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HcgBlogGenerator.ConsoleApp.Commands {
    public class BuildCommand : Command {
        private readonly ILogger<BuildCommand> _logger;
        private readonly IServiceProvider _serviceProvider; // Keep ServiceProvider to create scoped FileSystems
        private readonly ISiteBuilder _siteBuilder; // Inject SiteBuilder

        // --- Options --- (Use GlobalOptions)
        private static readonly Option<DirectoryInfo> SourceOption = GlobalOptions.SourceOption;
        private static readonly Option<DirectoryInfo> OutputOption = GlobalOptions.OutputOption;
        private static readonly Option<FileInfo> ConfigOption = GlobalOptions.ConfigOption;

        // Constructor receives dependencies via DI
        public BuildCommand(ILogger<BuildCommand> logger, IServiceProvider serviceProvider, ISiteBuilder siteBuilder)
            : base("build", "Builds the static site from source files.") {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _siteBuilder = siteBuilder; // Store injected dependency

            this.AddOption(SourceOption);
            this.AddOption(OutputOption);
            this.AddOption(ConfigOption);

            // Set the handler to the INSTANCE method ExecuteAsync
            //this.SetHandler((ctx) => ExecuteAsync(SourceOption, OutputOption, ConfigOption, ctx).GetAwaiter().GetResult());
        }

        // Instance method handler - receives bound option values
        private async Task<int> ExecuteAsync(DirectoryInfo sourceDirInfo, DirectoryInfo? outputDirInfo, FileInfo? configFileinfo, InvocationContext context) {
            var cancellationToken = context.GetCancellationToken();
            _logger.LogInformation("Build command executing...");

            // --- Resolve Paths ---
            string sourcePath = sourceDirInfo.FullName;
            string outputPath = outputDirInfo?.FullName ?? Path.Combine(sourcePath, "_site");
            string configPath = configFileinfo?.FullName ?? Path.Combine(sourcePath, SiteConstants.DefaultConfigFileName);

            _logger.LogDebug("Resolved Paths: Source='{Source}', Output='{Output}', Config='{Config}'", sourcePath, outputPath, configPath);

            // --- Validation ---
            if (!Directory.Exists(sourcePath)) { _logger.LogCritical("Source directory not found: {SourcePath}", sourcePath); return 1; }
            if (!File.Exists(configPath)) { _logger.LogCritical("Configuration file not found: {ConfigPath}", configPath); return 1; }
            try { Directory.CreateDirectory(outputPath); } catch (Exception ex) { _logger.LogCritical(ex, "Failed to create output directory: {OutputPath}", outputPath); return 1; }

            try {
                // --- Create IFileSystem instances ---
                // FileSystems are stateful based on root path, create new instances here.
                // Get required loggers from the provider.
                var sourceFs = new LocalFileSystem(sourcePath, false, _serviceProvider.GetRequiredService<ILogger<LocalFileSystem>>());
                var outputFs = new LocalFileSystem(outputPath, true, _serviceProvider.GetRequiredService<ILogger<LocalFileSystem>>());

                // --- Use injected SiteBuilder ---
                await _siteBuilder.BuildAsync(configPath, sourceFs, outputFs, cancellationToken);

                _logger.LogInformation("Build command finished successfully.");
                return 0; // Success code
            }
            catch (OperationCanceledException) {
                _logger.LogWarning("Build cancelled.");
                return 130; // Standard cancellation code
            }
            catch (Exception ex) {
                _logger.LogCritical(ex, "An unexpected error occurred during the build process.");
                return 1; // Error code
            }
        }
    }
}
