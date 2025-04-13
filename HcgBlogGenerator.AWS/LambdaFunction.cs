using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents; // For HTTP API events
using Amazon.S3;
using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Utilities; // For AddHcgBlogGeneratorCore, SiteConstants
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO; // For Path
using System.Net; // For HttpStatusCode
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HcgBlogGenerator.Aws;

public class Function {
    private static readonly IServiceProvider _serviceProvider;
    private static readonly LambdaConfiguration _lambdaConfig;
    private static readonly ILogger<Function> _logger;

    // Static constructor runs once per cold start
    static Function() {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables() // Load environment variables
            .Build();

        // Bind environment variables to our configuration class
        _lambdaConfig = config.Get<LambdaConfiguration>() ?? new LambdaConfiguration();

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection, config);
        _serviceProvider = serviceCollection.BuildServiceProvider();

        // Get logger after service provider is built
        _logger = _serviceProvider.GetRequiredService<ILogger<Function>>();

        // Validate essential configuration
        if (string.IsNullOrWhiteSpace(_lambdaConfig.SOURCE_BUCKET) || string.IsNullOrWhiteSpace(_lambdaConfig.OUTPUT_BUCKET)) {
            _logger.LogCritical("Lambda is misconfigured. SOURCE_BUCKET and OUTPUT_BUCKET environment variables are required.");
            // Subsequent invocations will fail until config is fixed.
        }
        else {
            _logger.LogInformation("Lambda function initialized. Source: s3://{Bucket}/{Prefix}, Output: s3://{OutBucket}/{OutPrefix}",
               _lambdaConfig.SOURCE_BUCKET, _lambdaConfig.SOURCE_PREFIX, _lambdaConfig.OUTPUT_BUCKET, _lambdaConfig.OUTPUT_PREFIX);
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration) {
        // --- Logging ---
        services.AddLogging(loggingBuilder => {
            loggingBuilder.ClearProviders(); // Remove default providers if any added by runtime
            loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
            // Use LambdaLoggerProvider (provided by Amazon.Lambda.Logging.AspNetCore in web apps, but here we use basic console/core logging)
            // The Lambda runtime captures Console.WriteLine, so standard Console logger works.
            loggingBuilder.AddConsole(options => options.LogToStandardErrorThreshold = Microsoft.Extensions.Logging.LogLevel.Debug); // Log Info+ to stderr for CloudWatch
        });

        // Register configuration object if needed by other services
        services.AddSingleton(configuration);
        services.AddSingleton(_lambdaConfig); // Register specific Lambda config

        // --- AWS Clients ---
        // The Lambda execution role should provide credentials
        services.AddAWSService<IAmazonS3>(); // Registers default S3 client

        // --- HcgBlogGenerator Core Services ---
        // Placeholder paths - actual paths are determined at runtime by the handler
        services.AddHcgBlogGeneratorCore();

        // No need to register AwsS3FileSystem itself here, as we create instances dynamically
    }

    /// <summary>
    /// Lambda function handler triggered by API Gateway HTTP API (v2 payload).
    /// </summary>
    /// <param name="apiProxyRequest">The API Gateway request object.</param>
    /// <param name="context">The Lambda context object.</param>
    /// <returns>An API Gateway response object.</returns>
    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(
        APIGatewayHttpApiV2ProxyRequest apiProxyRequest,
        ILambdaContext context) {
        _logger.LogInformation("Received HTTP request. Method: {HttpMethod}, Path: {Path}", apiProxyRequest.RequestContext?.Http?.Method, apiProxyRequest.RequestContext?.Http?.Path);
        // Log correlation IDs if available
        _logger.LogDebug("Lambda Request ID: {AwsRequestId}, API Gateway Request ID: {ApiRequestId}", context.AwsRequestId, apiProxyRequest.RequestContext?.RequestId);

                // --- Configuration Validation (Runtime) ---
        // Re-check critical config on each invocation in case environment changed (unlikely but possible)
        if (string.IsNullOrWhiteSpace(_lambdaConfig.SOURCE_BUCKET) || string.IsNullOrWhiteSpace(_lambdaConfig.OUTPUT_BUCKET)) {
            _logger.LogError("Lambda function is missing required environment variable configuration (SOURCE_BUCKET or OUTPUT_BUCKET).");
            return CreateErrorResponse(HttpStatusCode.InternalServerError, "Lambda function misconfigured.");
        }

        // Determine source/output details (could potentially be overridden by request payload if designed that way)
        string sourceBucket = _lambdaConfig.SOURCE_BUCKET;
        string sourcePrefix = _lambdaConfig.SOURCE_PREFIX ?? ""; // Default to root if not set
        string outputBucket = _lambdaConfig.OUTPUT_BUCKET;
        string outputPrefix = _lambdaConfig.OUTPUT_PREFIX ?? ""; // Default to root if not set
        string configFileKey = Path.Combine(sourcePrefix, _lambdaConfig.CONFIG_FILE_KEY ?? SiteConstants.DefaultConfigFileName)
                                   .Replace('\\', '/'); // S3 key for config


        // Simple security check (e.g., require a specific path or header?) - Optional
        // if (apiProxyRequest.RequestContext?.Http?.Path != "/build") {
        //     return CreateErrorResponse(HttpStatusCode.NotFound, "Not Found");
        // }

        var cancellationTokenSource = new CancellationTokenSource(); // Potential timeout later?

        try {
            // --- Create IFileSystem instances ---
            _logger.LogDebug("Creating S3 FileSystem instances...");
            var s3Client = _serviceProvider.GetRequiredService<IAmazonS3>();
            var sourceFs = new AwsS3FileSystem(s3Client, sourceBucket, sourcePrefix, _serviceProvider.GetRequiredService<ILogger<AwsS3FileSystem>>());
            var outputFs = new AwsS3FileSystem(s3Client, outputBucket, outputPrefix, _serviceProvider.GetRequiredService<ILogger<AwsS3FileSystem>>());

            // --- Get SiteBuilder service ---
            var siteBuilder = _serviceProvider.GetRequiredService<ISiteBuilder>();

            _logger.LogInformation("Initiating site build...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // --- Execute Build ---
            // Use the configFileKey relative to the source FS root (which is empty for AwsS3FileSystem constructor)
            // The AwsS3FileSystem prepends the bucket prefix internally.
            string configPathInSourceFs = configFileKey.StartsWith(sourcePrefix)
                ? configFileKey.Substring(sourcePrefix.Length)
                : configFileKey; // Path relative to source prefix

            await siteBuilder.BuildAsync(configPathInSourceFs.TrimStart('/'), sourceFs, outputFs, cancellationTokenSource.Token);

            stopwatch.Stop();
            _logger.LogInformation("Site build completed successfully in {ElapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);

            return new APIGatewayHttpApiV2ProxyResponse {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonSerializer.Serialize(new { message = "Site build triggered and completed successfully." }),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
        catch (FileNotFoundException fnfEx) {
            _logger.LogError(fnfEx, "Configuration file or essential source file not found.");
            return CreateErrorResponse(HttpStatusCode.BadRequest, $"Configuration file or essential source file not found: {fnfEx.Message}");
        }
        catch (OperationCanceledException) {
            _logger.LogWarning("Build operation was cancelled (possibly timed out).");
            return CreateErrorResponse(HttpStatusCode.InternalServerError, "Build operation cancelled or timed out.");
        }
        catch (Exception ex) {
            _logger.LogCritical(ex, "An unexpected error occurred during the site build triggered by HTTP request.");
            return CreateErrorResponse(HttpStatusCode.InternalServerError, $"Internal Server Error: {ex.Message}");
        }
    }

    private APIGatewayHttpApiV2ProxyResponse CreateErrorResponse(HttpStatusCode statusCode, string message) {
        return new APIGatewayHttpApiV2ProxyResponse {
            StatusCode = (int)statusCode,
            Body = JsonSerializer.Serialize(new { error = message }),
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }
}
