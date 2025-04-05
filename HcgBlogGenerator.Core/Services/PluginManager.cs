using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Models;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Manages the discovery and execution of registered plugins during the build process.
/// </summary>
public class PluginManager {
    private readonly ILogger<PluginManager> _logger;
    private readonly IEnumerable<IPlugin> _plugins; // Injected collection of all registered plugins

    /// <summary>
    /// Initializes a new instance of the PluginManager.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="plugins">An enumerable collection of all IPlugin instances registered in the DI container.</param>
    public PluginManager(ILogger<PluginManager> logger, IEnumerable<IPlugin> plugins) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _plugins = plugins ?? Enumerable.Empty<IPlugin>(); // Handle empty collection

        if (!_plugins.Any()) {
            _logger.LogInformation("PluginManager initialized. No plugins were registered.");
        }
        else {
            var pluginNames = string.Join(", ", _plugins.Select(p => p.Name ?? "Unnamed Plugin"));
            _logger.LogInformation("PluginManager initialized with {PluginCount} plugins: [{PluginNames}]", _plugins.Count(), pluginNames);
        }
    }

    /// <summary>
    /// Executes all registered plugins applicable to the specified pipeline stage.
    /// </summary>
    /// <param name="stage">The pipeline stage to execute plugins for.</param>
    /// <param name="siteContext">The current site context.</param>
    /// <param name="sourceFileSystem">Filesystem for reading source files.</param>
    /// <param name="outputFileSystem">Filesystem for writing output files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the completion of all applicable plugin executions for the stage.</returns>
    public async Task RunPluginsAsync(
        PipelineStage stage,
        SiteContext siteContext,
        IFileSystem sourceFileSystem,
        IFileSystem outputFileSystem,
        CancellationToken cancellationToken = default) {
        if (!_plugins.Any()) {
            _logger.LogDebug("No plugins registered, skipping execution for stage: {Stage}", stage);
            return;
        }

        _logger.LogInformation("--- Running Plugins for Stage: {Stage} ---", stage);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        int executedCount = 0;

        // Execute plugins sequentially for predictability, though parallel could be an option if plugins are independent.
        foreach (var plugin in _plugins) {
            cancellationToken.ThrowIfCancellationRequested();
            var pluginName = plugin.Name ?? plugin.GetType().Name; // Fallback name
            _logger.LogDebug("Executing plugin '{PluginName}' for stage {Stage}...", pluginName, stage);

            try {
                // It's the plugin's responsibility to check the stage internally if needed,
                // but the manager calls Execute for all plugins for the given stage.
                await plugin.ExecuteAsync(stage, siteContext, sourceFileSystem, outputFileSystem, cancellationToken);
                executedCount++;
                _logger.LogTrace("Plugin '{PluginName}' executed successfully for stage {Stage}.", pluginName, stage);
            }
            catch (OperationCanceledException) {
                _logger.LogWarning("Plugin '{PluginName}' execution cancelled for stage {Stage}.", pluginName, stage);
                throw; // Re-throw cancellation
            }
            catch (Exception ex) {
                // Log error but continue with other plugins for robustness? Or halt?
                // Let's log and continue for now. Add configuration later if needed.
                _logger.LogError(ex, "Error executing plugin '{PluginName}' during stage {Stage}. Continuing with other plugins.", pluginName, stage);
            }
        }

        stopwatch.Stop();
        _logger.LogInformation("--- Finished Plugins for Stage: {Stage} ({ExecutedCount} executed) in {ElapsedMilliseconds} ms ---", stage, executedCount, stopwatch.ElapsedMilliseconds);
    }
}
