// --- HcgBlogGenerator.Core/Interfaces/IPluginManager.cs ---

using HcgBlogGenerator.Core.Models;

namespace HcgBlogGenerator.Core.Interfaces;

/// <summary>
/// Defines the contract for discovering, loading, and executing plugins.
/// </summary>
public interface IPluginManager {
    /// <summary>
    /// Loads plugins based on configuration or discovery mechanisms.
    /// </summary>
    /// <param name="siteConfiguration">Site configuration which might contain plugin settings.</param>
    /// <returns>A collection of loaded build lifecycle plugins.</returns>
    Task<IEnumerable<IBuildLifecyclePlugin>> LoadPluginsAsync(SiteConfiguration siteConfiguration);

    /// <summary>
    /// Executes a specific lifecycle hook on all applicable loaded plugins.
    /// </summary>
    /// <param name="plugins">The collection of loaded plugins.</param>
    /// <param name="hookAction">An action representing the specific hook to invoke on each plugin (e.g., p => p.ExecuteBeforeBuildAsync(context)).</param>
    /// <param name="context">The context object to pass to the plugin hook.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous execution of the hook across all plugins.</returns>
    Task ExecutePluginsAsync(
        IEnumerable<IBuildLifecyclePlugin> plugins,
        Func<IBuildLifecyclePlugin, PluginContext, CancellationToken, Task> hookAction,
        PluginContext context,
        CancellationToken cancellationToken = default);
}
