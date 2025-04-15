using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer; // Required for TransferUtility
using Amazon.S3.Util; // Required for AmazonS3Util

using HcgBlogGenerator.Core.Abstractions;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net; // For HttpStatusCode
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HcgBlogGenerator.Aws;

/// <summary>
/// Implements IFileSystem for interaction with an AWS S3 bucket.
/// Treats objects within a specific prefix as the "root" directory.
/// Uses forward slashes '/' as path separators, matching S3 object key conventions.
/// </summary>
public class AwsS3FileSystem : IFileSystem {
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _rootPrefix; // e.g., "source/" or "output/" or "" for root
    private readonly ILogger<AwsS3FileSystem> _logger;
    private readonly TransferUtility _transferUtility; // For potentially easier uploads/downloads

    public string RootPath => $"s3://{_bucketName}/{_rootPrefix}"; // Conceptual RootPath

    /// <summary>
    /// Creates a new instance of AwsS3FileSystem.
    /// </summary>
    /// <param name="s3Client">Initialized IAmazonS3 client.</param>
    /// <param name="bucketName">Name of the S3 bucket.</param>
    /// <param name="rootPrefix">
    /// The prefix within the bucket to treat as the root directory.
    /// Should end with '/' if specifying a folder structure, or be empty for bucket root.
    /// Example: "source/", "output/generated/", "".
    /// </param>
    /// <param name="logger">Logger instance.</param>
    public AwsS3FileSystem(IAmazonS3 s3Client, string bucketName, string rootPrefix, ILogger<AwsS3FileSystem> logger) {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Normalize prefix: ensure it ends with '/' if not empty, handle null/whitespace
        if (string.IsNullOrWhiteSpace(rootPrefix)) {
            _rootPrefix = "";
        }
        else {
            _rootPrefix = rootPrefix.Trim().Replace('\\', '/');
            if (!_rootPrefix.EndsWith('/')) {
                _rootPrefix += '/';
            }
        }

        _transferUtility = new TransferUtility(_s3Client); // Initialize TransferUtility

        _logger.LogDebug("AwsS3FileSystem initialized for Bucket: '{Bucket}', Prefix: '{Prefix}'", _bucketName, _rootPrefix);
    }


    private string GetFullS3Key(string relativePath) {
        // Combine root prefix with the relative path (already using '/')
        // Ensure no double slashes if relativePath starts with '/'
        string cleanRelativePath = relativePath.TrimStart('/');
        return _rootPrefix + cleanRelativePath;
    }

    // --- Read Operations ---

    public async Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default) {
        string key = GetFullS3Key(path);
        _logger.LogTrace("S3 Check File Exists: {Key}", key);
        try {
            // HEAD Object is efficient for checking existence & metadata
            await _s3Client.GetObjectMetadataAsync(_bucketName, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            return false;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error checking S3 object existence for Key: {Key}", key);
            throw; // Re-throw other exceptions
        }
    }

    public async Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default) {
        // In S3, directories don't explicitly exist. We check if any objects have the directory prefix.
        // Ensure the path ends with a slash to represent a directory prefix.
        string prefix = GetFullS3Key(path.TrimEnd('/') + "/");
        _logger.LogTrace("S3 Check Dir Exists (Prefix check): {Prefix}", prefix);

        var request = new ListObjectsV2Request {
            BucketName = _bucketName,
            Prefix = prefix,
            MaxKeys = 1 // We only need to know if at least one object exists with this prefix
        };

        try {
            ListObjectsV2Response response = await _s3Client.ListObjectsV2Async(request, cancellationToken);
            return response.S3Objects.Any();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error checking S3 directory existence (listing objects) for Prefix: {Prefix}", prefix);
            throw;
        }
    }

    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) {
        string key = GetFullS3Key(path);
        _logger.LogTrace("S3 Read Text: {Key}", key);
        try {
            GetObjectRequest request = new GetObjectRequest { BucketName = _bucketName, Key = key };
            using GetObjectResponse response = await _s3Client.GetObjectAsync(request, cancellationToken);
            using StreamReader reader = new StreamReader(response.ResponseStream, Encoding.UTF8); // Assume UTF-8
            return await reader.ReadToEndAsync();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            _logger.LogError("S3 object not found for ReadAllTextAsync: {Key}", key);
            throw new FileNotFoundException($"S3 object not found: {key}", key, ex);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error reading S3 object content for Key: {Key}", key);
            throw;
        }
    }

    public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) {
        string key = GetFullS3Key(path);
        _logger.LogTrace("S3 Read Bytes: {Key}", key);
        try {
            GetObjectRequest request = new GetObjectRequest { BucketName = _bucketName, Key = key };
            using GetObjectResponse response = await _s3Client.GetObjectAsync(request, cancellationToken);
            using MemoryStream ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms, cancellationToken);
            return ms.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            _logger.LogError("S3 object not found for ReadAllBytesAsync: {Key}", key);
            throw new FileNotFoundException($"S3 object not found: {key}", key, ex);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error reading S3 object content (bytes) for Key: {Key}", key);
            throw;
        }
    }

    public async Task<Stream> OpenReadStreamAsync(string path, CancellationToken cancellationToken = default) {
        string key = GetFullS3Key(path);
        _logger.LogTrace("S3 Open Read Stream: {Key}", key);
        try {
            // GetObjectAsync returns a response containing the stream.
            // The caller MUST dispose the response AND the stream.
            // This isn't ideal as the IFileSystem interface implies caller only disposes the stream.
            // Option 1: Return response.ResponseStream directly (caller must be careful or use using).
            // Option 2: Copy to MemoryStream (inefficient for large files).
            // Option 3: Custom Stream wrapper that disposes response (complex).

            // Let's go with Option 1 for now, document the requirement.
            GetObjectRequest request = new GetObjectRequest { BucketName = _bucketName, Key = key };
            GetObjectResponse response = await _s3Client.GetObjectAsync(request, cancellationToken);
            // Potential issue: Response gets disposed when caller disposes stream if not careful.
            // A wrapper stream is safer but adds complexity.
            _logger.LogWarning("OpenReadStreamAsync for S3 returns the direct response stream. Ensure the stream AND the response object wrapping it are disposed correctly by the caller, or copy the stream content if needed.");
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            _logger.LogError("S3 object not found for OpenReadStreamAsync: {Key}", key);
            throw new FileNotFoundException($"S3 object not found: {key}", key, ex);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error opening S3 object read stream for Key: {Key}", key);
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetFilesAsync(string path, string searchPattern, bool recursive, CancellationToken cancellationToken = default) {
        // S3 ListObjectsV2 with Prefix simulates directory listing. Search pattern is not directly supported server-side.
        // We list objects and filter client-side if needed (inefficient for complex patterns).
        // For simple "*.md" type patterns, we can sometimes filter by Suffix, but ListObjectsV2 doesn't support it directly.
        // We will list all objects under the prefix and filter locally.

        string prefix = GetFullS3Key(path.TrimEnd('/') + "/"); // Ensure prefix ends with /
        _logger.LogTrace("S3 Get Files: Prefix='{Prefix}', Recursive={Recursive}", prefix, recursive);

        var results = new List<string>();
        var request = new ListObjectsV2Request {
            BucketName = _bucketName,
            Prefix = prefix,
            // Delimiter = recursive ? null : "/" // Use delimiter ONLY if NOT recursive to get immediate children
            Delimiter = recursive ? null : "/"
        };

        // Basic wildcard support for simple patterns like *.md
        Func<string, bool> filter = key => true; // Default: accept all
        if (searchPattern.StartsWith("*.")) {
            string extension = searchPattern.Substring(1); // Includes the dot
            filter = key => key.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
            _logger.LogDebug("Applying client-side filter for pattern: {Pattern}", searchPattern);
        }
        else if (!string.IsNullOrEmpty(searchPattern) && searchPattern != "*.*" && searchPattern != "*") {
            // Log warning for unsupported complex patterns
            _logger.LogWarning("Complex search patterns like '{Pattern}' are not efficiently supported by S3 GetFilesAsync. Listing all and filtering may be inefficient.", searchPattern);
            // Consider regex matching if needed:
            // var regex = new Regex(WildcardToRegex(searchPattern)); filter = key => regex.IsMatch(Path.GetFileName(key));
        }


        try {
            ListObjectsV2Response response;
            do {
                _logger.LogDebug("Calling ListObjectsV2Async. Bucket: '{Bucket}', Prefix: '{Prefix}', Delimiter: '{Delimiter}', Token: '{Token}'",
                    request.BucketName, request.Prefix, request.Delimiter, request.ContinuationToken);
                response = await _s3Client.ListObjectsV2Async(request, cancellationToken);
                _logger.LogTrace("ListObjectsV2Async returned {Count} objects in this page. IsTruncated: {Truncated}", response.S3Objects.Count, response.IsTruncated);

                foreach (S3Object obj in response.S3Objects) {
                    // Make path relative to the root prefix
                    string relativePath = obj.Key.StartsWith(_rootPrefix)
                        ? obj.Key.Substring(_rootPrefix.Length)
                        : obj.Key; // Should not happen if prefix logic is correct

                    _logger.LogTrace("S3Object {Key} got converted to {relativePath} with root as {rootPrefix}", obj.Key, relativePath, _rootPrefix);

                    // Exclude objects that represent folders themselves (ending in '/')
                    // Also apply client-side filtering
                    if (!string.IsNullOrEmpty(relativePath) && !relativePath.EndsWith('/')) {
                        _logger.LogTrace("Adding path to results: {Path}", relativePath);
                        results.Add(relativePath);
                    }
                    else {
                        _logger.LogTrace("Skipping path: {Path} (IsNullOrEmpty={IsNull}, EndsWithSlash={EndsSlash})",
                            relativePath, string.IsNullOrEmpty(relativePath), relativePath?.EndsWith('/') ?? false); // Log why skipped
                    }
                }
                request.ContinuationToken = response.NextContinuationToken;

            } while (response.IsTruncated);

            _logger.LogDebug("Finished listing for prefix '{Prefix}'. Total results found matching filter: {Count}", request.Prefix, results.Count);
            return results;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error listing S3 objects for Prefix: {Prefix}", prefix);
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetDirectoriesAsync(string path, CancellationToken cancellationToken = default) {
        // Use ListObjectsV2 with a Delimiter to find common prefixes (folders)
        string prefix = GetFullS3Key(path.TrimEnd('/') + "/");
        _logger.LogTrace("S3 Get Directories: Prefix='{Prefix}'", prefix);

        var results = new List<string>();
        var request = new ListObjectsV2Request {
            BucketName = _bucketName,
            Prefix = prefix,
            Delimiter = "/" // Use delimiter to group by folder
        };

        try {
            ListObjectsV2Response response;
            do {
                response = await _s3Client.ListObjectsV2Async(request, cancellationToken);

                foreach (string commonPrefix in response.CommonPrefixes) {
                    // Make path relative to the root prefix
                    string relativePath = commonPrefix.StartsWith(_rootPrefix)
                        ? commonPrefix.Substring(_rootPrefix.Length)
                        : commonPrefix;

                    // Remove trailing slash for directory name consistency
                    results.Add(relativePath.TrimEnd('/'));
                }
                request.ContinuationToken = response.NextContinuationToken;

            } while (response.IsTruncated);

            return results;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error listing S3 common prefixes (directories) for Prefix: {Prefix}", prefix);
            throw;
        }
    }


    // --- Write Operations ---

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default) {
        string key = GetFullS3Key(path);
        _logger.LogTrace("S3 Write Text: {Key}", key);
        // Determine content type (useful for serving from S3)
        string contentType = GetContentType(path);

        // Use PutObject for simple text writes
        var request = new PutObjectRequest {
            BucketName = _bucketName,
            Key = key,
            ContentBody = content, // SDK handles encoding (UTF-8 by default)
            ContentType = contentType
            // TODO: Add ACL, Metadata, Caching headers if needed
        };
        return _s3Client.PutObjectAsync(request, cancellationToken);
    }

    public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default) {
        string key = GetFullS3Key(path);
        _logger.LogTrace("S3 Write Bytes: {Key}", key);
        string contentType = GetContentType(path);

        // Use PutObject with MemoryStream
        using var stream = new MemoryStream(bytes);
        var request = new PutObjectRequest {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType
        };
        return _s3Client.PutObjectAsync(request, cancellationToken);
    }

    public Task WriteStreamAsync(string path, Stream stream, CancellationToken cancellationToken = default) {
        string key = GetFullS3Key(path);
        _logger.LogTrace("S3 Write Stream: {Key}", key);
        string contentType = GetContentType(path);

        // Use TransferUtility for potentially more robust stream uploads (handles multi-part for large streams)
        var request = new TransferUtilityUploadRequest {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            // AutoCloseStream = false, // Let the caller manage stream lifecycle? Default is true. Set false if stream needs reuse.
            AutoResetStreamPosition = stream.CanSeek // Automatically seek to beginning if possible
        };
        return _transferUtility.UploadAsync(request, cancellationToken);
    }

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) {
        // In S3, directories are implicitly created when an object is placed within them.
        // Some tools create zero-byte objects ending in '/' to represent empty folders explicitly.
        // Let's create such an object if the path doesn't correspond to an existing object/prefix.
        if (string.IsNullOrWhiteSpace(path))
            return Task.CompletedTask; // Cannot create root

        string key = GetFullS3Key(path.TrimEnd('/') + "/"); // Ensure ends with slash
        _logger.LogTrace("S3 Create Directory (Placeholder Object): {Key}", key);

        // Check if it already exists as a folder marker or if objects exist under it?
        // Optimization: Only create if needed? Let's just ensure the placeholder exists.
        var request = new PutObjectRequest {
            BucketName = _bucketName,
            Key = key,
            ContentBody = string.Empty // Zero-byte object
        };
        return _s3Client.PutObjectAsync(request, cancellationToken);
    }

    public async Task DeleteFileAsync(string path, CancellationToken cancellationToken = default) {
        string key = GetFullS3Key(path);
        _logger.LogTrace("S3 Delete File: {Key}", key);
        var request = new DeleteObjectRequest {
            BucketName = _bucketName,
            Key = key
        };
        // DeleteObjectAsync doesn't throw NotFound exception if key doesn't exist
        await _s3Client.DeleteObjectAsync(request, cancellationToken);
    }

    public async Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default) {
        // Deleting a "directory" in S3 means deleting all objects with that prefix.
        if (!recursive) {
            _logger.LogWarning("Non-recursive directory delete is not directly supported in S3 via this method. It only deletes the directory placeholder object if it exists.");
            // Optionally delete only the placeholder object key ending in "/"
            string key = GetFullS3Key(path.TrimEnd('/') + "/");
            await DeleteFileAsync(key.Substring(_rootPrefix.Length), cancellationToken); // Delete placeholder
            return;
        }

        string prefix = GetFullS3Key(path.TrimEnd('/') + "/");
        _logger.LogTrace("S3 Recursive Delete Directory (Prefix): {Prefix}", prefix);

        // List all objects with the prefix
        var request = new ListObjectsV2Request { BucketName = _bucketName, Prefix = prefix };
        ListObjectsV2Response response;
        var keysToDelete = new List<KeyVersion>();

        _logger.LogInformation("Listing objects for deletion under prefix: {Prefix}...", prefix);
        do {
            response = await _s3Client.ListObjectsV2Async(request, cancellationToken);
            keysToDelete.AddRange(response.S3Objects.Select(o => new KeyVersion { Key = o.Key }));
            request.ContinuationToken = response.NextContinuationToken;
            _logger.LogDebug("Found {Count} objects in current batch for deletion.", response.S3Objects.Count);
        } while (response.IsTruncated);

        if (!keysToDelete.Any()) {
            _logger.LogInformation("No objects found under prefix {Prefix} to delete.", prefix);
            // Also delete placeholder?
            await DeleteFileAsync(path.TrimEnd('/') + "/", cancellationToken); // Attempt to delete placeholder anyway
            return;
        }

        // Delete objects in batches (max 1000 per request)
        _logger.LogInformation("Deleting {TotalCount} objects under prefix {Prefix}...", keysToDelete.Count, prefix);
        for (int i = 0; i < keysToDelete.Count; i += 1000) {
            var batch = keysToDelete.Skip(i).Take(1000).ToList();
            var deleteRequest = new DeleteObjectsRequest {
                BucketName = _bucketName,
                Objects = batch,
                Quiet = true // Suppress individual results unless errors occur
            };
            DeleteObjectsResponse deleteResponse = await _s3Client.DeleteObjectsAsync(deleteRequest, cancellationToken);
            if (deleteResponse.DeleteErrors.Any()) {
                foreach (var error in deleteResponse.DeleteErrors) {
                    _logger.LogError("Error deleting S3 object {Key}: {Code} - {Message}", error.Key, error.Code, error.Message);
                }
                // Decide whether to throw or just log errors
                // throw new Exception($"Failed to delete some objects under prefix {prefix}. See logs for details.");
            }
            _logger.LogDebug("Deleted batch of {BatchCount} objects.", batch.Count);
        }
        _logger.LogInformation("Finished deleting objects under prefix {Prefix}.", prefix);
    }

    public Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite, CancellationToken cancellationToken = default) {
        // S3 CopyObject operation. Overwrite is implicit if destination key exists.
        string sourceKey = GetFullS3Key(sourcePath);
        string destKey = GetFullS3Key(destinationPath);
        _logger.LogTrace("S3 Copy File: {SourceKey} -> {DestKey}", sourceKey, destKey);

        // Basic check: Cannot copy object onto itself
        if (sourceKey.Equals(destKey, StringComparison.Ordinal)) {
            _logger.LogWarning("Source and destination keys are the same for copy operation: {Key}", sourceKey);
            return Task.CompletedTask;
        }

        var request = new CopyObjectRequest {
            SourceBucket = _bucketName,
            SourceKey = sourceKey,
            DestinationBucket = _bucketName,
            DestinationKey = destKey,
            // TODO: Add ACL, MetadataDirective, Caching headers if needed
        };

        // If overwrite is FALSE, we need to check if destKey exists first.
        if (!overwrite) {
            // Need an async Task wrapper for this check
            return CopyFileIfNotExistsAsync(request, cancellationToken);
        }
        else {
            // Standard copy, overwrites implicitly
            return _s3Client.CopyObjectAsync(request, cancellationToken);
        }
    }

    private async Task CopyFileIfNotExistsAsync(CopyObjectRequest request, CancellationToken cancellationToken) {
        try {
            // Check if destination exists
            await _s3Client.GetObjectMetadataAsync(request.DestinationBucket, request.DestinationKey, cancellationToken);
            // If above line doesn't throw NotFound, it exists. Log and return.
            _logger.LogWarning("Destination key {DestKey} already exists and overwrite is false. Skipping copy.", request.DestinationKey);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            // Destination doesn't exist, proceed with copy
            _logger.LogDebug("Destination key {DestKey} does not exist. Proceeding with copy.", request.DestinationKey);
            await _s3Client.CopyObjectAsync(request, cancellationToken);
        }
        // Let other exceptions propagate
    }

    // --- Utility ---

    public char PathSeparator => '/';

    public string CombinePath(params string[] paths) {
        // Combine using internal separator '/', handling segments carefully
        return string.Join("/", paths
            .Select(p => p?.Replace('\\', '/').Trim('/')) // Normalize and trim slashes from each part
            .Where(p => !string.IsNullOrEmpty(p))); // Remove empty parts
    }

    private string GetContentType(string path) {
        // Basic content type detection based on extension
        string extension = Path.GetExtension(path).ToLowerInvariant();
        // Use built-in utility if available, otherwise basic map
        string mimeType = AmazonS3Util.MimeTypeFromExtension(extension);

        if (!string.IsNullOrEmpty(mimeType)) {
            return mimeType;
        }

        // Basic fallback map
        switch (extension) {
            case ".html":
            case ".htm":
                return "text/html; charset=utf-8";
            case ".css":
                return "text/css; charset=utf-8";
            case ".js":
                return "application/javascript; charset=utf-8";
            case ".json":
                return "application/json; charset=utf-8";
            case ".xml":
                return "application/xml; charset=utf-8";
            case ".txt":
                return "text/plain; charset=utf-8";
            case ".png":
                return "image/png";
            case ".jpg":
            case ".jpeg":
                return "image/jpeg";
            case ".gif":
                return "image/gif";
            case ".svg":
                return "image/svg+xml";
            case ".woff2":
                return "font/woff2";
            case ".ico":
                return "image/x-icon";
            default:
                return "application/octet-stream"; // Default binary type
        }
    }
}
