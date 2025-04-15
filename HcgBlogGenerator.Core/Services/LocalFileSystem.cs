using System.Text;

using HcgBlogGenerator.Core.Abstractions;

using Microsoft.Extensions.Logging;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Implements IFileSystem for interaction with the local operating system's file system.
/// Normalizes paths internally but uses System.IO which expects OS-specific paths.
/// Targets .NET 8+, using modern async file methods directly.
/// </summary>
public class LocalFileSystem : IFileSystem {
    private readonly string _rootPath;
    private readonly ILogger<LocalFileSystem> _logger;

    // Store the OS-specific separator for conversions
    private static readonly char _osSeparator = Path.DirectorySeparatorChar;
    private const char InternalSeparator = '/'; // Use '/' internally

    /// <summary>
    /// Creates a new instance of LocalFileSystem operating within a specific root directory.
    /// </summary>
    /// <param name="rootPath">The absolute base path for all operations. All relative paths passed to methods will be relative to this root.</param>
    /// <param name="logger">Logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if rootPath or logger is null.</exception>
    /// <exception cref="ArgumentException">Thrown if rootPath is not an absolute path.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown if the rootPath does not exist.</exception>
    public LocalFileSystem(string rootPath, bool forceClean, ILogger<LocalFileSystem> logger) {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentNullException(nameof(rootPath));
        if (!Path.IsPathRooted(rootPath))
            throw new ArgumentException("Root path must be absolute.", nameof(rootPath));

        // Ensure the root directory exists
        if (forceClean) {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, true);
            Directory.CreateDirectory(rootPath);
        }

        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"The specified root path does not exist: {rootPath}");

        _rootPath = Path.GetFullPath(rootPath); // Normalize the root path
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("LocalFileSystem initialized with root: {RootPath}", _rootPath);
    }

    // --- Read Operations ---

    public Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default) {
        string fullPath = GetFullPath(path);
        _logger.LogDebug("Checking file existence: {Path}", fullPath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default) {
        string fullPath = GetFullPath(path);
        _logger.LogDebug("Checking directory existence: {Path}", fullPath);
        return Task.FromResult(Directory.Exists(fullPath));
    }

    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) {
        string fullPath = GetFullPath(path);
        _logger.LogDebug("Reading text file: {Path}", fullPath);
        try {
            return await File.ReadAllTextAsync(fullPath, Encoding.UTF8, cancellationToken);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error reading text file: {Path}", fullPath);
            throw; // Re-throw exceptions like FileNotFoundException, IOException etc.
        }
    }

    public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) {
        string fullPath = GetFullPath(path);
        _logger.LogDebug("Reading byte file: {Path}", fullPath);
        try {
            return await File.ReadAllBytesAsync(fullPath, cancellationToken);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error reading byte file: {Path}", fullPath);
            throw;
        }
    }

    public Task<Stream> OpenReadStreamAsync(string path, CancellationToken cancellationToken = default) {
        string fullPath = GetFullPath(path);
        _logger.LogDebug("Opening read stream: {Path}", fullPath);
        try {
            // File.OpenRead opens with FileAccess.Read, FileShare.Read
            // Using FileStream constructor for async option
            Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            return Task.FromResult(stream);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error opening read stream: {Path}", fullPath);
            throw;
        }
    }

    public Task<IEnumerable<string>> GetFilesAsync(string path, string searchPattern, bool recursive, CancellationToken cancellationToken = default) {
        string fullPath = GetFullPath(path);
        _logger.LogDebug("Getting files from: {Path} (Pattern: {Pattern}, Recursive: {Recursive})", fullPath, searchPattern, recursive);
        try {
            if (!Directory.Exists(fullPath)) {
                _logger.LogWarning("Directory not found for GetFilesAsync: {Path}", fullPath);
                return Task.FromResult(Enumerable.Empty<string>());
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            // EnumerateFiles executes lazily, ToList forces execution and converts paths
            var files = Directory.EnumerateFiles(fullPath, searchPattern, searchOption)
                                 .Select(MakeRelativePath) // Convert back to relative paths with '/'
                                 .ToList();
            return Task.FromResult<IEnumerable<string>>(files);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error getting files from: {Path}", fullPath);
            throw;
        }
    }

    public Task<IEnumerable<string>> GetDirectoriesAsync(string path, CancellationToken cancellationToken = default) {
        string fullPath = GetFullPath(path);
        _logger.LogDebug("Getting directories from: {Path}", fullPath);
        try {
            if (!Directory.Exists(fullPath)) {
                _logger.LogWarning("Directory not found for GetDirectoriesAsync: {Path}", fullPath);
                return Task.FromResult(Enumerable.Empty<string>());
            }

            var directories = Directory.EnumerateDirectories(fullPath)
                                       .Select(MakeRelativePath) // Convert back to relative paths with '/'
                                       .ToList();
            return Task.FromResult<IEnumerable<string>>(directories);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error getting directories from: {Path}", fullPath);
            throw;
        }
    }


    // --- Write Operations ---

    public async Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default) {
        string fullPath = GetFullPath(path);
        EnsureDirectoryExists(Path.GetDirectoryName(fullPath));
        _logger.LogDebug("Writing text file: {Path}", fullPath);
        try {
            await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8, cancellationToken);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error writing text file: {Path}", fullPath);
            throw;
        }
    }

    public async Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default) {
        string fullPath = GetFullPath(path);
        EnsureDirectoryExists(Path.GetDirectoryName(fullPath));
        _logger.LogDebug("Writing byte file: {Path}", fullPath);
        try {
            await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error writing byte file: {Path}", fullPath);
            throw;
        }
    }

    public async Task WriteStreamAsync(string path, Stream stream, CancellationToken cancellationToken = default) {
        string fullPath = GetFullPath(path);
        EnsureDirectoryExists(Path.GetDirectoryName(fullPath));
        _logger.LogDebug("Writing stream to file: {Path}", fullPath);
        try {
            // Open file stream for writing, ensure stream is copied correctly
            using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true)) {
                // If the input stream is seekable, reset its position
                if (stream.CanSeek) {
                    stream.Seek(0, SeekOrigin.Begin);
                }
                await stream.CopyToAsync(fileStream, cancellationToken);
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error writing stream to file: {Path}", fullPath);
            throw;
        }
    }

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) {
        string fullPath = GetFullPath(path);
        _logger.LogDebug("Creating directory (if not exists): {Path}", fullPath);
        try {
            // Directory.CreateDirectory handles cases where the directory already exists
            // and creates parent directories as needed.
            Directory.CreateDirectory(fullPath);
            return Task.CompletedTask;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error creating directory: {Path}", fullPath);
            throw;
        }
    }

    public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default) {
        string fullPath = GetFullPath(path);
        _logger.LogDebug("Deleting file: {Path}", fullPath);
        try {
            if (File.Exists(fullPath)) {
                File.Delete(fullPath);
                _logger.LogTrace("Deleted file: {Path}", fullPath);
            }
            else {
                _logger.LogWarning("Attempted to delete non-existent file: {Path}", fullPath);
            }
            return Task.CompletedTask;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error deleting file: {Path}", fullPath);
            throw;
        }
    }

    public Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default) {
        string fullPath = GetFullPath(path);
        _logger.LogDebug("Deleting directory: {Path} (Recursive: {Recursive})", fullPath, recursive);
        try {
            if (Directory.Exists(fullPath)) {
                Directory.Delete(fullPath, recursive);
                _logger.LogTrace("Deleted directory: {Path}", fullPath);
            }
            else {
                _logger.LogWarning("Attempted to delete non-existent directory: {Path}", fullPath);
            }
            return Task.CompletedTask;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error deleting directory: {Path}", fullPath);
            throw;
        }
    }

    public Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite, CancellationToken cancellationToken = default) {
        string fullSourcePath = GetFullPath(sourcePath);
        string fullDestPath = GetFullPath(destinationPath);
        EnsureDirectoryExists(Path.GetDirectoryName(fullDestPath));
        _logger.LogDebug("Copying file from {Source} to {Destination} (Overwrite: {Overwrite})", fullSourcePath, fullDestPath, overwrite);
        try {
            File.Copy(fullSourcePath, fullDestPath, overwrite);
            return Task.CompletedTask;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error copying file from {Source} to {Destination}", fullSourcePath, fullDestPath);
            throw;
        }
    }

    // --- Utility ---

    public char PathSeparator => InternalSeparator; // We prefer '/' internally

    public string RootPath => this._rootPath;

    public string CombinePath(params string[] paths) {
        // Combine using internal separator, handling potential leading/trailing separators
        // Filter out null/empty segments before joining
        return string.Join(InternalSeparator.ToString(), paths.Where(p => !string.IsNullOrEmpty(p)).Select(p => p.Trim(InternalSeparator)));
    }

    // --- Private Helpers ---

    /// <summary>
    /// Converts a relative path (using '/') to a full, OS-specific path based on the root.
    /// Also performs basic security check against path traversal.
    /// </summary>
    private string GetFullPath(string relativePath) {
        // Treat null or empty as the root itself? Or throw? Let's assume root.
        if (string.IsNullOrEmpty(relativePath)) return _rootPath;

        // Normalize internal separator to OS separator
        string osRelativePath = relativePath.Replace(InternalSeparator, _osSeparator);

        // Combine with root
        string fullPath = Path.Combine(_rootPath, osRelativePath);

        // Normalize the resulting path (handles ., ..) and ensure it's still within the root
        fullPath = Path.GetFullPath(fullPath);

        // Security check: Ensure the resulting path is still within the root directory
        // Using OrdinalIgnoreCase for cross-platform compatibility (Windows paths are case-insensitive)
        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase) && fullPath != _rootPath) // Allow exact match for root
        {
            _logger.LogError("Path traversal attempt detected or path escaped root: Relative='{RelativePath}', Resolved='{FullPath}', Root='{RootPath}'", relativePath, fullPath, _rootPath);
            throw new UnauthorizedAccessException($"Path access denied: '{relativePath}' resolves outside the allowed root directory.");
        }

        return fullPath;
    }

    /// <summary>
    /// Converts an absolute OS-specific path back to a relative path (using '/') based on the root.
    /// </summary>
    private string MakeRelativePath(string fullPath) {
        // Ensure the full path is indeed under the root path (case-insensitive check)
        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase)) {
            _logger.LogWarning("Cannot make path relative as it's outside the root: {FullPath}", fullPath);
            // Decide on behavior: return original, empty, or throw? Returning original seems safest for now.
            return fullPath.Replace(_osSeparator, InternalSeparator);
        }

        // Handle case where fullPath *is* the root path
        if (Path.GetFullPath(fullPath).Equals(Path.GetFullPath(_rootPath), StringComparison.OrdinalIgnoreCase)) {
            return string.Empty; // Relative path of the root is empty
        }


        // Get relative path (might start with OS separator depending on OS)
        string relative = Path.GetRelativePath(_rootPath, fullPath);

        // Normalize to internal separator
        return relative.Replace(_osSeparator, InternalSeparator);
    }


    /// <summary>
    /// Ensures the directory for a given file path exists, creating it if necessary.
    /// Expects an absolute OS-specific directory path.
    /// </summary>
    private void EnsureDirectoryExists(string? directoryPath) {
        // Check if directoryPath is null or empty (can happen if GetDirectoryName returns null for root paths)
        if (string.IsNullOrEmpty(directoryPath)) {
            return;
        }

        if (!Directory.Exists(directoryPath)) {
            _logger.LogTrace("Creating directory: {DirectoryPath}", directoryPath);
            Directory.CreateDirectory(directoryPath);
        }
    }
}
