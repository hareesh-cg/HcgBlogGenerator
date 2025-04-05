using HcgBlogGenerator.ConsoleApp.Commands;
using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Services;
using HcgBlogGenerator.Core.Utilities; // For AddHcgBlogGeneratorCore

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace HcgBlogGenerator.ConsoleApp {
    public class Program {
        public static async Task<int> Main(string[] args) {
            // await BuildNow("D:\\Projects\\Websites\\Test\\testing3", "D:\\Projects\\Websites\\Test\\result3");
            await ServeNow("D:\\Projects\\CSharp\\HcgBlogGenerator\\samples\\sample-blog", "D:\\Projects\\Websites\\Test\\result3", 3000);
            // return await ExecuteCommand(args);

            return 0;
        }

        public static async Task<int> ServeNow(string sourcePath, string outputPath, int port) {
            string configPath = Path.Combine(sourcePath, SiteConstants.DefaultConfigFileName);

            // --- Configure Dependency Injection ---
            var services = new ServiceCollection();

            // 1. Configure Logging
            services.AddLogging(configure => {
                configure.SetMinimumLevel(LogLevel.Information);
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

        public static async Task<int> ExecuteCommand(string[] args) {
            // --- Build Command Line Parser with Hosting ---
            var parser = new CommandLineBuilder(CreateRootCommand()) // Pass RootCommand factory method
                .UseDefaults() // Includes help, version, etc.
                .UseHost(_ => Host.CreateDefaultBuilder(args), // Use Generic Host
                    (builder) => // Configure Host Builder
                    {
                        builder.ConfigureLogging((context, logging) => {
                            logging.ClearProviders();
                            logging.AddConsole();
                            // logging.SetMinimumLevel(LogLevel.Information);
                        });

                        builder.ConfigureServices((hostContext, services) => {
                            // --- Register Core Generator Services ---
                            services.AddHcgBlogGeneratorCore();

                            // --- Register CLI Specific Services ---
                            services.AddTransient<DevelopmentServer>();

                            // --- Register Command CLASSES for DI ---
                            // System.CommandLine.Hosting uses these registrations to create instances
                            // when the corresponding command is invoked.
                            services.AddTransient<BuildCommand>();
                            services.AddTransient<ServeCommand>();
                            // services.AddTransient<InitCommand>();
                        });
                    })
                .Build();

            // --- Execute ---
            return await parser.InvokeAsync(args);
        }

        // Helper method to create the RootCommand and add registered commands
        private static RootCommand CreateRootCommand() {
            var rootCommand = new RootCommand("HcgBlogGenerator: A static site generator for blogs.");

            // Commands are added here, BUT their instances and handlers
            // will be managed by the DI container and UseHost extension.
            // We need to create dummy instances here just to add them to the root command definition.
            // Their handlers will be properly resolved later.

            // Get command instances from DI to add to RootCommand
            // Note: This requires the DI container to be available *before* building the parser
            // which is not the case with the standard UseHost pattern.
            // Let's define commands directly instead.

            // Define commands directly and add them. Handlers are set within command constructors.
            rootCommand.AddCommand(new BuildCommand(null!, null!, null!)); // Pass dummy nulls, DI will provide real ones to handler instance
            rootCommand.AddCommand(new ServeCommand(null!, null!)); // Pass dummy nulls

            // A cleaner way using AddCommand<TCommand> from UseHost / DI setup:
            // The UseHost extension actually makes registered commands available.
            // We might not need to manually add commands if they are registered in DI? Let's test this.

            // REVISED Approach: Let UseHost handle command addition if registered in DI
            // builder.ConfigureServices((context, services) => {
            //    services.AddTransient<BuildCommand>(); // Register class
            // });
            // builder.UseCommandHandler<BuildCommand, BuildCommand.Handler>(); // Link command to handler (if handler is separate class)

            // Simpler: Just register Command classes in DI. UseHost should find them?
            // Let's rely on the command constructors setting their own handlers and adding options.

            // We need *some* way to tell RootCommand about the commands.
            // Let's define the structure here and let DI hydrate the handlers.
            var buildCommand = new Command("build", "Builds the static site from source files.");
            buildCommand.AddOption(GlobalOptions.SourceOption);
            buildCommand.AddOption(GlobalOptions.OutputOption);
            buildCommand.AddOption(GlobalOptions.ConfigOption);
            // Handler is set inside BuildCommand constructor now

            var serveCommand = new Command("serve", "Serves the generated site locally for development.");
            serveCommand.AddOption(GlobalOptions.OutputOption);
            serveCommand.AddOption(GlobalOptions.SourceOption);
            serveCommand.AddOption(Commands.ServeCommand.PortOption); // Use the static PortOption
                                                                      // Handler is set inside ServeCommand constructor now

            rootCommand.AddCommand(buildCommand);
            rootCommand.AddCommand(serveCommand);


            return rootCommand;
        }

    } // End Class Program
}
