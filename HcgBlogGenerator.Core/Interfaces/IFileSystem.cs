using System.Text;

namespace HcgBlogGenerator.Core.Interfaces;

/// <summary>
/// Defines an abstraction for file system operations, allowing for different implementations
/// (e.g., local disk, cloud storage) and facilitating testing.
/// Based on relevant async methods from System.IO.Abstractions.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Determines whether the specified file exists.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns>True if the file exists; otherwise, false.</returns>
    Task<bool> FileExistsAsync(string path);

    /// <summary>
    /// Determines whether the specified directory exists.
    /// </summary>
    /// <param name="path">The directory path to check.</param>
    /// <returns>True if the directory exists; otherwise, false.</returns>
    Task<bool> DirectoryExistsAsync(string path);

    /// <summary>
    /// Creates all directories and subdirectories in the specified path unless they already exist.
    /// </summary>
    /// <param name="path">The directory path to create.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task CreateDirectoryAsync(string path);

    /// <summary>
    /// Deletes the specified directory and, if indicated, any subdirectories and files in the directory.
    /// </summary>
    /// <param name="path">The path of the directory to remove.</param>
    /// <param name="recursive">True to remove directories, subdirectories, and files in path; otherwise, false.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task DeleteDirectoryAsync(string path, bool recursive);

    /// <summary>
    /// Opens a text file, reads all the text in the file, and then closes the file.
    /// </summary>
    /// <param name="path">The file to open for reading.</param>
    /// <param name="encoding">The encoding applied to the contents of the file. Defaults to UTF8.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A string containing all the text in the file.</returns>
    Task<string> ReadAllTextAsync(string path, Encoding? encoding = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new file, writes the specified string to the file, and then closes the file.
    /// If the target file already exists, it is overwritten.
    /// </summary>
    /// <param name="path">The file to write to.</param>
    /// <param name="contents">The string to write to the file.</param>
    /// <param name="encoding">The encoding to use. Defaults to UTF8.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task WriteAllTextAsync(string path, string contents, Encoding? encoding = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an existing file or creates a new file for writing.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="mode">Specifies how the operating system should open a file.</param>
    /// <returns>A stream associated with the specified path.</returns>
    Task<Stream> OpenWriteAsync(string path, FileMode mode = FileMode.Create);

    /// <summary>
    /// Opens an existing file for reading.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>A read-only stream associated with the specified path.</returns>
    Task<Stream> OpenReadAsync(string path);

    /// <summary>
    /// Copies an existing file to a new file. Overwriting a file of the same name is allowed.
    /// </summary>
    /// <param name="sourceFileName">The file to copy.</param>
    /// <param name="destFileName">The name of the destination file. This cannot be a directory.</param>
    /// <param name="overwrite">True if the destination file can be overwritten; otherwise, false.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task CopyFileAsync(string sourceFileName, string destFileName, bool overwrite, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the names of files (including their paths) that match the specified search pattern in the specified directory.
    /// </summary>
    /// <param name="path">The relative or absolute path to the directory to search. This string is not case-sensitive.</param>
    /// <param name="searchPattern">The search string to match against the names of files in path. This parameter can contain a combination of valid literal path and wildcard (* and ?) characters, but it doesn't support regular expressions.</param>
    /// <param name="searchOption">One of the enumeration values that specifies whether the search operation should include only the current directory or should include all subdirectories.</param>
    /// <returns>An asynchronous enumerable collection of the full names (including paths) for the files in the directory specified by path and that match the specified search pattern and option.</returns>
    IAsyncEnumerable<string> EnumerateFilesAsync(string path, string searchPattern, SearchOption searchOption);

    /// <summary>
    /// Returns the names of subdirectories (including their paths) that match the specified search pattern in the specified directory.
    /// </summary>
    /// <param name="path">The relative or absolute path to the directory to search. This string is not case-sensitive.</param>
    /// <param name="searchPattern">The search string to match against the names of subdirectories in path. This parameter can contain a combination of valid literal path and wildcard (* and ?) characters, but it doesn't support regular expressions.</param>
    /// <param name="searchOption">One of the enumeration values that specifies whether the search operation should include only the current directory or should include all subdirectories.</param>
    /// <returns>An asynchronous enumerable collection of the full names (including paths) for the directories in the directory specified by path and that match the specified search pattern and option.</returns>
    IAsyncEnumerable<string> EnumerateDirectoriesAsync(string path, string searchPattern, SearchOption searchOption);

    /// <summary>
    /// Gets the directory path separator character.
    /// </summary>
    char DirectorySeparatorChar { get; }

    /// <summary>
    /// Combines two strings into a path.
    /// </summary>
    /// <param name="path1">The first path to combine.</param>
    /// <param name="path2">The second path to combine.</param>
    /// <returns>The combined paths.</returns>
    string Combine(string path1, string path2);

    /// <summary>
    /// Returns the directory information for the specified path string.
    /// </summary>
    /// <param name="path">The path of a file or directory.</param>
    /// <returns>Directory information for path, or null if path denotes a root directory or is null. Returns String.Empty if path does not contain directory information.</returns>
    string? GetDirectoryName(string path);

    /// <summary>
    /// Returns the file name and extension of the specified path string.
    /// </summary>
    /// <param name="path">The path string from which to obtain the file name and extension.</param>
    /// <returns>The characters after the last directory separator character in path. If the last character of path is a directory or volume separator character, this method returns String.Empty. If path is null, this method returns null.</returns>
    string? GetFileName(string path);

     /// <summary>
    /// Returns the file name of the specified path string without the extension.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The string returned by GetFileName, minus the last period (.) and all characters following it.</returns>
    string? GetFileNameWithoutExtension(string path);

    /// <summary>
    /// Returns the absolute path for the specified path string.
    /// </summary>
    /// <param name="path">The file or directory for which to obtain absolute path information.</param>
    /// <returns>The fully qualified location of path, such as "C:\MyFile.txt".</returns>
    string GetFullPath(string path);

    /// <summary>
    /// Returns the extension (including the period ".") of the specified path string.
    /// </summary>
    /// <param name="path">The path string from which to get the extension.</param>
    /// <returns>The extension of the specified path (including the period "."), or null, or String.Empty. If path is null, returns null. If path does not have extension information, returns String.Empty.</returns>
    string? GetExtension(string path);
} 