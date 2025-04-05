using HcgBlogGenerator.Core.Services; // For LocalFileSystem

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HcgBlogGenerator.ConsoleApp.Commands {
    public class ServeCommand : Command {
        private readonly ILogger<ServeCommand> _logger;
        private readonly DevelopmentServer _devServer; // Inject DevServer

        // --- Define Options ---
        // Use GlobalOptions where applicable
        private static readonly Option<DirectoryInfo> OutputOption = GlobalOptions.OutputOption;
        private static readonly Option<DirectoryInfo> SourceOption = GlobalOptions.SourceOption;

        // Define PortOption specific to this command as public static readonly
        public static readonly Option<int> PortOption =
            new Option<int>(aliases: new[] { "--port", "-p" }, getDefaultValue: () => 8080, description: "Port number to serve the site on.");
        // TODO: Add --watch option later

        // Constructor receives dependencies via DI
        public ServeCommand(ILogger<ServeCommand> logger, DevelopmentServer devServer)
            : base("serve", "Serves the generated site locally for development.") {
            _logger = logger;
            _devServer = devServer; // Store injected dependency

            // Add options to the command definition
            this.AddOption(OutputOption);
            this.AddOption(SourceOption);
            this.AddOption(PortOption);

            // Set the handler to the INSTANCE method ExecuteAsync
            // System.CommandLine.Hosting will create an instance of this class via DI
            // and call this method, injecting bound option values into parameters.
            //this.SetHandler((ctx) => ExecuteAsync2(OutputOption, ctx)); // (ExecuteAsync, OutputOption, SourceOption, PortOption);
        }

        // Instance method handler - receives bound option values directly
        private async Task<int> ExecuteAsync(DirectoryInfo? outputDirInfo, DirectoryInfo sourceDirInfo, int port, InvocationContext context) {
            // Get CancellationToken from InvocationContext
            var cancellationToken = context.GetCancellationToken();

            _logger.LogInformation("Serve command executing...");

            // --- Resolve Output Path ---
            string sourcePath = sourceDirInfo.FullName;
            string outputPath = outputDirInfo?.FullName ?? Path.Combine(sourcePath, "_site");

            _logger.LogDebug("Serving site from directory: {OutputPath} on port {Port}", outputPath, port);

            if (!Directory.Exists(outputPath)) {
                _logger.LogError("Output directory not found: {OutputPath}", outputPath);
                _logger.LogInformation("Suggestion: Run the 'build' command first to generate the site.");
                return 1; // Error code
            }

            try {
                // DevServer is already available via constructor injection (_devServer)
                _logger.LogInformation("Starting local development server. Press CTRL+C to stop.");

                // Start the server and wait for it to complete (blocks until cancelled)
                await _devServer.StartAsync(outputPath, port, cancellationToken);

                _logger.LogInformation("Development server shut down gracefully.");
                return 0; // Success code
            }
            catch (OperationCanceledException) {
                _logger.LogInformation("Development server shut down gracefully via cancellation.");
                return 0; // Success
            }
            catch (Exception ex) {
                _logger.LogCritical(ex, "An unexpected error occurred while running the development server.");
                // Stop server might not be needed if StartAsync throws
                // await _devServer.StopAsync();
                return 1; // Error code
            }
        }
    }
}
