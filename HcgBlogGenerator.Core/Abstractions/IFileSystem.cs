using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HcgBlogGenerator.Core.Abstractions;

/// <summary>
/// Abstracts file and directory operations for both local and cloud storage.
/// Paths are expected to be relative to a defined root (e.g., site source root, output root).
/// Use forward slashes ('/') as path separators internally for consistency.
/// </summary>
public interface IFileSystem {
    // --- Read Operations ---

    string RootPath { get; }

    Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default);
    Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default);

    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadStreamAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets relative paths of files within a directory, optionally recursively.
    /// </summary>
    /// <param name="path">Directory path relative to the root.</param>
    /// <param name="searchPattern">Search pattern (e.g., "*.md").</param>
    /// <param name="recursive">Search recursively.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An enumerable collection of relative file paths.</returns>
    Task<IEnumerable<string>> GetFilesAsync(string path, string searchPattern, bool recursive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets relative paths of directories within a directory.
    /// </summary>
    /// <param name="path">Directory path relative to the root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An enumerable collection of relative directory paths.</returns>
    Task<IEnumerable<string>> GetDirectoriesAsync(string path, CancellationToken cancellationToken = default);


    // --- Write Operations ---

    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);
    Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default);
    Task WriteStreamAsync(string path, Stream stream, CancellationToken cancellationToken = default);

    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);

    Task DeleteFileAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a directory and optionally its contents recursively.
    /// </summary>
    Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a file from a source path to a destination path within the same filesystem context.
    /// </summary>
    Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite, CancellationToken cancellationToken = default);

    // --- Utility ---

    /// <summary>
    /// Gets the appropriate path separator for the underlying system (primarily for display or logging if needed).
    /// Internal logic should prefer '/'.
    /// </summary>
    char PathSeparator { get; }

    /// <summary>
    /// Combines path segments using the preferred internal separator '/'.
    /// </summary>
    string CombinePath(params string[] paths);
}
