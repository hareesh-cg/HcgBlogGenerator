using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace HcgBlogGenerator.ConsoleApp;

/// <summary>
/// Manages a local Kestrel development server for serving static files.
/// </summary>
public class DevelopmentServer {
    private readonly ILogger<DevelopmentServer> _logger;
    private IWebHost? _host;

    public DevelopmentServer(ILogger<DevelopmentServer> logger) {
        _logger = logger;
    }

    /// <summary>
    /// Starts the development server.
    /// </summary>
    /// <param name="directoryToServe">The absolute path to the directory containing the static files.</param>
    /// <param name="port">The port number to listen on.</param>
    /// <param name="cancellationToken">Token to signal server shutdown.</param>
    public Task StartAsync(string directoryToServe, int port, CancellationToken cancellationToken = default) {
        if (_host != null) {
            _logger.LogWarning("Development server is already running.");
            return Task.CompletedTask;
        }

        if (!Directory.Exists(directoryToServe)) {
            _logger.LogError("Directory to serve does not exist: {Directory}", directoryToServe);
            throw new DirectoryNotFoundException($"Directory not found: {directoryToServe}");
        }

        _logger.LogInformation("Starting development server for directory: {Directory} on port {Port}", directoryToServe, port);

        try {
            _host = new WebHostBuilder()
               .UseKestrel(options => {
                   options.Listen(IPAddress.Loopback, port); // Listen only on loopback
                   _logger.LogDebug("Kestrel listening on http://localhost:{Port}", port);
               })
               .UseContentRoot(directoryToServe) // Set content root for file provider
               .Configure(app => {
                   // Configure static file serving
                   var fileProvider = new PhysicalFileProvider(directoryToServe);
                   var staticFileOptions = new StaticFileOptions {
                       FileProvider = fileProvider,
                       // Optional: Set default file (e.g., index.html) - DefaultFilesMiddleware handles this better
                       // RequestPath = "" // Serve from root
                   };
                   // Set default file name (e.g., index.html)
                   app.UseDefaultFiles(new DefaultFilesOptions {
                       FileProvider = fileProvider,
                       DefaultFileNames = new List<string> { "index.html", "index.htm" }
                   });

                   app.UseStaticFiles(staticFileOptions); // Serve static files

                   _logger.LogDebug("Configured static file serving from {Directory}", directoryToServe);

                   // Optional: Add custom middleware here if needed (e.g., logging, live reload later)
               })
               .ConfigureLogging(logging => // Configure logging within the webhost if needed
               {
                   logging.AddConsole();
                   logging.SetMinimumLevel(LogLevel.Warning); // Keep webhost logging less verbose unless debugging
               })
               .Build();


            // Register cancellation token callback to stop the host
            cancellationToken.Register(() => {
                _logger.LogInformation("Shutdown signal received, stopping development server...");
                _host?.StopAsync().GetAwaiter().GetResult(); // Block slightly to ensure shutdown initiated
            });

            // Run the host. This is a blocking call until the host is shut down.
            // Run it asynchronously.
            return _host.RunAsync(cancellationToken);
        }
        catch (Exception ex) {
            _logger.LogCritical(ex, "Failed to start development server.");
            _host?.Dispose();
            _host = null;
            throw; // Re-throw the exception
        }
    }

    public async Task StopAsync() {
        if (_host != null) {
            _logger.LogInformation("Stopping development server...");
            await _host.StopAsync();
            _host.Dispose();
            _host = null;
            _logger.LogInformation("Development server stopped.");
        }
    }
}
