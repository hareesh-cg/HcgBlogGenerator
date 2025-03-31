// --- HcgBlogGenerator.Core/Interfaces/IAssetProcessor.cs ---

using HcgBlogGenerator.Core.Models;

namespace HcgBlogGenerator.Core.Interfaces;

/// <summary>
/// Defines the contract for processing static asset files.
/// </summary>
public interface IAssetProcessor {
    /// <summary>
    /// Processes a collection of asset files found during content discovery.
    /// This involves compiling SCSS, copying files, etc., based on the AssetType.
    /// </summary>
    /// <param name="assets">The collection of assets to process.</param>
    /// <param name="siteConfiguration">The site configuration, containing paths etc.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous processing operation.</returns>
    Task ProcessAssetsAsync(IEnumerable<Asset> assets, SiteConfiguration siteConfiguration, CancellationToken cancellationToken = default);
}
