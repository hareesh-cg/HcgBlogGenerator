using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Services;
using HcgBlogGenerator.Core.Utilities;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HcgBlogGenerator.ConsoleApp;

public class Program {
    public static async Task<int> Main(string[] args) {
        // await BuildNow("D:\\Projects\\Websites\\Test\\testing3", "D:\\Projects\\Websites\\Test\\result3");
        await ServeNow("D:\\Projects\\CSharp\\HcgBlogGenerator\\samples\\test-blog", "D:\\Projects\\Websites\\Test\\result3", 3000);
        // return await ExecuteCommand(args);

        return 0;
    }

    public static async Task<int> ServeNow(string sourcePath, string outputPath, int port) {
        string configPath = Path.Combine(sourcePath, SiteConstants.DefaultConfigFileName);

        // --- Configure Dependency Injection ---
        var services = new ServiceCollection();

        // 1. Configure Logging
        services.AddLogging(configure => {
            configure.SetMinimumLevel(LogLevel.Warning);
            configure.AddSimpleConsole(options => {
                options.IncludeScopes = true;
                options.TimestampFormat = "[HH:mm:ss] ";
                options.SingleLine = true; // Keeps logs concise
            });
        });

        // 2. Register Core Generator Services (includes default LocalFileSystem, plugins, etc.)
        services.AddHcgBlogGeneratorCore();
        services.AddTransient<DevelopmentServer>();

        // --- Build Service Provider and Execute ---
        await using var serviceProvider = services.BuildServiceProvider();

        // Create a DI scope for Scoped services (SiteGeneratorService, Processors, etc.)
        using (var scope = serviceProvider.CreateScope()) {
            var scopedProvider = scope.ServiceProvider;
            var logger = scopedProvider.GetRequiredService<ILogger<Program>>(); // Get logger for this class

            try {
                // Resolve the main service within the scope
                var siteBuilder = scopedProvider.GetRequiredService<ISiteBuilder>();                    

                // Execute the generation process
                var sourceFs = new LocalFileSystem(sourcePath, false, serviceProvider.GetRequiredService<ILogger<LocalFileSystem>>());
                var outputFs = new LocalFileSystem(outputPath, true, serviceProvider.GetRequiredService<ILogger<LocalFileSystem>>());

                // --- Use injected SiteBuilder ---
                await siteBuilder.BuildAsync(configPath, sourceFs, outputFs);

                var devServer = scopedProvider.GetRequiredService<DevelopmentServer>();
                await devServer.StartAsync(outputPath, port);

                logger.LogInformation("Build command finished successfully.");
                return 0; // Success exit code
            }
            catch (Exception ex) {
                logger.LogCritical(ex, "An unhandled exception occurred during site generation.");
                // Print details to console as well for immediate visibility
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("\n--- ERROR ---");
                Console.Error.WriteLine($"Error Type: {ex.GetType().Name}");
                Console.Error.WriteLine($"Message: {ex.Message}");
                Console.Error.WriteLine("Stack Trace:");
                Console.Error.WriteLine(ex.StackTrace);
                Console.ResetColor();
                return 1; // Failure exit code
            }
        }
    }

    public static async Task<int> BuildNow(string sourcePath, string outputPath) {
        string configPath = Path.Combine(sourcePath, SiteConstants.DefaultConfigFileName);

        // --- Configure Dependency Injection ---
        var services = new ServiceCollection();

        // 1. Configure Logging
        services.AddLogging(configure =>
        {
            configure.SetMinimumLevel(LogLevel.Information);
            configure.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "[HH:mm:ss] ";
                options.SingleLine = true; // Keeps logs concise
            });
        });

        // 2. Register Core Generator Services (includes default LocalFileSystem, plugins, etc.)
        services.AddHcgBlogGeneratorCore();

        // --- Build Service Provider and Execute ---
        await using var serviceProvider = services.BuildServiceProvider();

        // Create a DI scope for Scoped services (SiteGeneratorService, Processors, etc.)
        using (var scope = serviceProvider.CreateScope()) {
            var scopedProvider = scope.ServiceProvider;
            var logger = scopedProvider.GetRequiredService<ILogger<Program>>(); // Get logger for this class

            try {
                // Resolve the main service within the scope
                var siteBuilder = scopedProvider.GetRequiredService<ISiteBuilder>();

                // Execute the generation process
                var sourceFs = new LocalFileSystem(sourcePath, false, serviceProvider.GetRequiredService<ILogger<LocalFileSystem>>());
                var outputFs = new LocalFileSystem(outputPath, true, serviceProvider.GetRequiredService<ILogger<LocalFileSystem>>());

                // --- Use injected SiteBuilder ---
                await siteBuilder.BuildAsync(configPath, sourceFs, outputFs);

                logger.LogInformation("Build command finished successfully.");
                return 0; // Success exit code
            }
            catch (Exception ex) {
                logger.LogCritical(ex, "An unhandled exception occurred during site generation.");
                // Print details to console as well for immediate visibility
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("\n--- ERROR ---");
                Console.Error.WriteLine($"Error Type: {ex.GetType().Name}");
                Console.Error.WriteLine($"Message: {ex.Message}");
                Console.Error.WriteLine("Stack Trace:");
                Console.Error.WriteLine(ex.StackTrace);
                Console.ResetColor();
                return 1; // Failure exit code
            }
        }
    }
} // End Class Program
