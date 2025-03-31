// --- HcgBlogGenerator.Core/Interfaces/ILocalWebServer.cs ---

namespace HcgBlogGenerator.Core.Interfaces;

/// <summary>
/// Defines the contract for a local development web server used by the 'serve' command.
/// Implementations will typically host the generated site from the output directory.
/// </summary>
public interface ILocalWebServer : IAsyncDisposable {
    /// <summary>
    /// Starts the local web server asynchronously.
    /// </summary>
    /// <param name="directoryToServe">The absolute path to the directory containing the static site files.</param>
    /// <param name="port">The port number to listen on.</param>
    /// <param name="cancellationToken">A token to signal server shutdown.</param>
    /// <returns>A task representing the server's execution. This task typically completes when the server is stopped.</returns>
    Task StartAsync(string directoryToServe, int port, CancellationToken cancellationToken);

    // Note: Stopping is handled via the CancellationToken and IAsyncDisposable.DisposeAsync()
}
