using System.Net; // For HttpStatusCode
using System.Text.Json;

using Amazon.Lambda.APIGatewayEvents; // For HTTP API events
using Amazon.Lambda.Core;
using Amazon.S3;

using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Utilities; // For AddHcgBlogGeneratorCore, SiteConstants

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HcgBlogGenerator.Aws;

public class Function {
    private static readonly IServiceProvider _serviceProvider;
    private static readonly ILogger<Function> _logger;

    // Static constructor runs once per cold start
    static Function() {
        // Configuration can still load general settings (e.g., logging levels) from Env Vars
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection, config); // Pass general config
        _serviceProvider = serviceCollection.BuildServiceProvider();

        // Get logger after service provider is built
        _logger = _serviceProvider.GetRequiredService<ILogger<Function>>();
        
        _logger.LogInformation("Lambda function initialized.");
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration) {
        services.AddLogging(loggingBuilder => {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
            // The Lambda runtime captures Console.WriteLine, so standard Console logger works.
            loggingBuilder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
        });

        // Register configuration if needed elsewhere
        services.AddSingleton(configuration);

        // --- AWS Clients ---
        // The Lambda execution role should provide credentials
        services.AddAWSService<IAmazonS3>(); // Registers default S3 client

        // --- HcgBlogGenerator Core Services ---
        services.AddHcgBlogGeneratorCore();
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

        // --- Method Validation ---
        if (!apiProxyRequest.RequestContext?.Http?.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) ?? true) {
            _logger.LogWarning("Received non-POST request ({Method}). Rejecting.", apiProxyRequest.RequestContext?.Http?.Method ?? "UNKNOWN");
            return CreateErrorResponse(HttpStatusCode.MethodNotAllowed, "Only POST requests are allowed.");
        }

        // Simple security check (e.g., require a specific path or header?) - Optional
        // if (apiProxyRequest.RequestContext?.Http?.Path != "/build") {
        //     return CreateErrorResponse(HttpStatusCode.NotFound, "Not Found");
        // }

        // --- Deserialize Request Body ---
        BuildRequest? buildRequest = null;
        if (string.IsNullOrWhiteSpace(apiProxyRequest.Body)) {
            _logger.LogError("Request body is missing or empty.");
            return CreateErrorResponse(HttpStatusCode.BadRequest, "Request body is required.");
        }
        try {
            // Handle base64 encoding if API Gateway is configured that way (common for binary/non-JSON)
            // Assuming standard JSON payload here. Adjust if using base64.
            string requestBody = apiProxyRequest.IsBase64Encoded
                ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(apiProxyRequest.Body))
                : apiProxyRequest.Body;

            buildRequest = JsonSerializer.Deserialize<BuildRequest>(requestBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); // Allow camelCase/PascalCase variations

            if (buildRequest == null)
                throw new JsonException("Deserialized request is null.");
        }
        catch (JsonException jsonEx) {
            _logger.LogError(jsonEx, "Failed to deserialize request body JSON.");
            return CreateErrorResponse(HttpStatusCode.BadRequest, $"Invalid JSON request body: {jsonEx.Message}");
        }
        catch (FormatException formatEx) // Catch potential Base64 decoding errors
        {
            _logger.LogError(formatEx, "Failed to decode base64 request body.");
            return CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid base64 request body.");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error processing request body.");
            return CreateErrorResponse(HttpStatusCode.BadRequest, "Could not process request body.");
        }

        // --- Parameter Validation (from Request Body) ---
        if (string.IsNullOrWhiteSpace(buildRequest.SourceBucket) || string.IsNullOrWhiteSpace(buildRequest.OutputBucket)) {
            _logger.LogError("Request body is missing required fields 'source_bucket' or 'output_bucket'.");
            return CreateErrorResponse(HttpStatusCode.BadRequest, "Request body must include 'source_bucket' and 'output_bucket'.");
        }

        // --- Use Parameters from Request ---
        string sourceBucket = buildRequest.SourceBucket;
        string sourcePrefix = buildRequest.SourcePrefix ?? ""; // Default to root if not set
        string outputBucket = buildRequest.OutputBucket;
        string outputPrefix = buildRequest.OutputPrefix ?? ""; // Default to root if not set
        string configFileKey = Path.Combine(sourcePrefix, buildRequest.ConfigFileKey ?? SiteConstants.DefaultConfigFileName)
                                    .Replace('\\', '/'); // S3 key for config

        var cancellationTokenSource = new CancellationTokenSource(); // Potential timeout later?

        try {
            // --- Create IFileSystem instances ---
            _logger.LogDebug("Creating S3 FileSystem instances...");
            var s3Client = _serviceProvider.GetRequiredService<IAmazonS3>();
            var sourceFs = new AwsS3FileSystem(s3Client, sourceBucket, sourcePrefix, _serviceProvider.GetRequiredService<ILogger<AwsS3FileSystem>>());
            var outputFs = new AwsS3FileSystem(s3Client, outputBucket, outputPrefix, _serviceProvider.GetRequiredService<ILogger<AwsS3FileSystem>>());

            // --- Get SiteBuilder service ---
            var siteBuilder = _serviceProvider.GetRequiredService<ISiteBuilder>();

            _logger.LogInformation("Initiating site build from request. Source: s3://{Bucket}/{Prefix}, Output: s3://{OutBucket}/{OutPrefix}, Config: {Config}",
                  sourceBucket, sourcePrefix, outputBucket, outputPrefix, configFileKey);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // --- Execute Build ---
            // Use the configFileKey relative to the source FS root (which is empty for AwsS3FileSystem constructor)
            // The AwsS3FileSystem prepends the bucket prefix internally.
            string configPathInSourceFs = configFileKey.StartsWith(sourcePrefix) && !string.IsNullOrEmpty(sourcePrefix)
                ? configFileKey.Substring(sourcePrefix.Length)
                : configFileKey; // Path relative to source prefix

            await siteBuilder.BuildAsync(configPathInSourceFs.TrimStart('/'), sourceFs, outputFs, cancellationTokenSource.Token);

            stopwatch.Stop();
            _logger.LogInformation("Site build completed successfully in {ElapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);

            return new APIGatewayHttpApiV2ProxyResponse {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonSerializer.Serialize(new { version = "0.1.5", message = "Site build triggered and completed successfully." }),
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
