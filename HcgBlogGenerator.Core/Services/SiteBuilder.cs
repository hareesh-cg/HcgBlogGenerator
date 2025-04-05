using System.Text.Json; // Required for JSON deserialization
using System.Text.RegularExpressions; // Required for slug generation

using HcgBlogGenerator.Core.Abstractions;
using HcgBlogGenerator.Core.Models;
using HcgBlogGenerator.Core.Utilities;

using Microsoft.Extensions.Logging;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Implements ISiteBuilder to orchestrate the static site generation process.
/// </summary>
public class SiteBuilder : ISiteBuilder {
    private readonly ILogger<SiteBuilder> _logger;
    private readonly IContentParser _contentParser;
    private readonly ITemplateEngine _templateEngine;
    private readonly ICssCompiler _cssCompiler;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly PluginManager _pluginManager;
    // Add other services as needed: IMetadataExtractor, IReadingTimeCalculator, PluginManager etc.

    public SiteBuilder(
        ILogger<SiteBuilder> logger,
        IContentParser contentParser,
        ITemplateEngine templateEngine,
        ICssCompiler cssCompiler,
        IMetadataExtractor metadataExtractor,
        PluginManager pluginManager) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contentParser = contentParser ?? throw new ArgumentNullException(nameof(contentParser));
        _templateEngine = templateEngine ?? throw new ArgumentNullException(nameof(templateEngine));
        _cssCompiler = cssCompiler ?? throw new ArgumentNullException(nameof(cssCompiler));
        _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));
        _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
        _logger.LogDebug("SiteBuilder initialized.");
    }

    /// <summary>
    /// Builds the static site.
    /// </summary>
    public async Task BuildAsync(string configPath, IFileSystem sourceFileSystem, IFileSystem outputFileSystem, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Starting static site build process...");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        SiteConfiguration? configuration = null;
        SiteContext? siteContext = null;

        try {
            // 1. Load Configuration
            _logger.LogDebug("Loading configuration from: {ConfigPath}", configPath);
            configuration = await LoadConfigurationAsync(configPath, sourceFileSystem, cancellationToken);
            if (configuration == null) {
                _logger.LogCritical("Site configuration could not be loaded. Aborting build.");
                return;
            }
            ValidateConfiguration(configuration);

            // 2. Initialize Site Context (after config loaded)
            siteContext = new SiteContext(configuration);

            // --- Run PreBuild Plugins ---
            await _pluginManager.RunPluginsAsync(PipelineStage.PreBuild, siteContext, sourceFileSystem, outputFileSystem, cancellationToken);

            // 3. Initialize Output Directory
            await outputFileSystem.CreateDirectoryAsync(string.Empty, cancellationToken);

            // 4. Initialize Template Engine
            _logger.LogInformation("Initializing template engine...");
            await _templateEngine.InitializeAsync(configuration, sourceFileSystem, cancellationToken);

            // 5. Discover and Process Content
            _logger.LogInformation("Discovering and processing content from: {ContentDirectory}", configuration.ContentDirectory);
            await ProcessContentFilesAsync(siteContext, sourceFileSystem, cancellationToken);

            // --- Run PostContentProcessing Plugins ---
            await _pluginManager.RunPluginsAsync(PipelineStage.PostContentProcessing, siteContext, sourceFileSystem, outputFileSystem, cancellationToken);

            // 6. Post-Processing (Sorting, Relationships, Taxonomies, Pagination)
            _logger.LogInformation("Performing post-processing...");
            PerformPostProcessing(siteContext);

            // 7. Process Taxonomies (Generate taxonomy pages)
            await GenerateListPagesAsync(siteContext, cancellationToken);

            // TODO: 8. Process Paginated Lists (Generate list pages)
            // await ProcessPaginationAsync(siteContext, outputFileSystem, cancellationToken);

            // 9. Render Content Pages
            _logger.LogInformation("Rendering content pages...");
            await RenderContentItemsAsync(siteContext.Posts, siteContext, outputFileSystem, cancellationToken);
            await RenderContentItemsAsync(siteContext.Pages, siteContext, outputFileSystem, cancellationToken);
            await RenderContentItemsAsync(siteContext.ListPages, siteContext, outputFileSystem, cancellationToken);
            // TODO: Run PostRender Plugins here if implemented (needs careful thought on data flow)
            await _pluginManager.RunPluginsAsync(PipelineStage.PostRender, siteContext, sourceFileSystem, outputFileSystem, cancellationToken);

            // 10. Compile CSS
            _logger.LogInformation("Compiling CSS...");
            await CompileAndWriteCssAsync(configuration, sourceFileSystem, outputFileSystem, cancellationToken);

            // 11. Copy Static Files
            _logger.LogInformation("Copying static files from: {StaticDirectory}", configuration.StaticDirectory);
            await CopyStaticFilesAsync(configuration.StaticDirectory, sourceFileSystem, outputFileSystem, cancellationToken);

            // --- Run PostBuild Plugins ---
            // Run AFTER essential files (content, css, static) are written
            await _pluginManager.RunPluginsAsync(PipelineStage.PostBuild, siteContext, sourceFileSystem, outputFileSystem, cancellationToken);

            stopwatch.Stop();
            // --- Run BuildComplete Plugins --- (Run even before final success log)
            await _pluginManager.RunPluginsAsync(PipelineStage.BuildComplete, siteContext, sourceFileSystem, outputFileSystem, cancellationToken);
            _logger.LogInformation("Site build completed successfully in {ElapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) {
            stopwatch.Stop();
            _logger.LogWarning("Site build cancelled.");
            // Optionally run BuildComplete plugins even on cancellation? Requires careful thought.
            // if (siteContext != null) await _pluginManager.RunPluginsAsync(PipelineStage.BuildComplete, siteContext, sourceFileSystem, outputFileSystem, CancellationToken.None); // Use CancellationToken.None if needed
        }
        catch (Exception ex) {
            stopwatch.Stop();
            _logger.LogCritical(ex, "Critical error during site build process after {ElapsedMilliseconds} ms. Build may be incomplete.", stopwatch.ElapsedMilliseconds);
            // Optionally run BuildComplete plugins even on failure?
            // if (siteContext != null) await _pluginManager.RunPluginsAsync(PipelineStage.BuildComplete, siteContext, sourceFileSystem, outputFileSystem, CancellationToken.None);
            throw;
        }
    }

    // --- Helper Methods ---

    private async Task<SiteConfiguration?> LoadConfigurationAsync(string configPath, IFileSystem fileSystem, CancellationToken cancellationToken) {
        if (!await fileSystem.FileExistsAsync(configPath, cancellationToken)) {
            _logger.LogError("Configuration file not found: {ConfigPath}", configPath);
            return null;
        }

        try {
            var configJson = await fileSystem.ReadAllTextAsync(configPath, cancellationToken);
            // Use System.Text.Json for deserialization
            var options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true, // Allow case variations in JSON keys
                ReadCommentHandling = JsonCommentHandling.Skip, // Allow comments in JSON
            };
            var config = JsonSerializer.Deserialize<SiteConfiguration>(configJson, options);

            // Perform basic normalization/defaults if needed
            config.BaseUrl = config.BaseUrl?.TrimEnd('/') ?? string.Empty;
            // Normalize directory paths? (e.g., ensure they don't start/end with '/')

            _logger.LogInformation("Configuration loaded successfully.");
            return config;
        }
        catch (JsonException jsonEx) {
            _logger.LogError(jsonEx, "Failed to parse configuration file {ConfigPath}. Invalid JSON.", configPath);
            return null;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error reading or deserializing configuration file: {ConfigPath}", configPath);
            return null;
        }
    }

    private void ValidateConfiguration(SiteConfiguration config) {
        _logger.LogDebug("Validating configuration...");
        // Basic checks - add more as needed
        if (string.IsNullOrWhiteSpace(config.Title)) _logger.LogWarning("Site Title is not set in configuration.");
        if (string.IsNullOrWhiteSpace(config.BaseUrl)) _logger.LogWarning("Site BaseUrl is not set in configuration. Absolute URLs may not work correctly.");
        if (config.PostsPerPage <= 0) {
            _logger.LogWarning("PostsPerPage is invalid ({Value}), defaulting to 10.", config.PostsPerPage);
            config.PostsPerPage = 10;
        }
        // Check if directories likely exist? (Maybe too slow/complex here)
        _logger.LogDebug("Configuration validation complete (basic checks).");
    }

    private async Task ProcessContentFilesAsync(SiteContext siteContext, IFileSystem sourceFileSystem, CancellationToken cancellationToken) {
        var config = siteContext.Configuration;
        IEnumerable<string> contentFiles;

        try {
            // Find all markdown files recursively within the content directory
            contentFiles = await sourceFileSystem.GetFilesAsync(config.ContentDirectory, "*.md", true, cancellationToken);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error discovering content files in {ContentDirectory}", config.ContentDirectory);
            return; // Stop processing if discovery fails
        }


        int processedCount = 0;
        int skippedDraftCount = 0;
        int skippedFutureCount = 0;

        foreach (var filePath in contentFiles) {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Processing content file: {FilePath}", filePath);

            try {
                string rawContent = await sourceFileSystem.ReadAllTextAsync(filePath, cancellationToken);
                var parseResult = await _contentParser.ParseAsync(rawContent, filePath, cancellationToken);

                // Check for Draft status
                if (parseResult.FrontMatter.Draft && !config.BuildDrafts) {
                    skippedDraftCount++;
                    _logger.LogDebug("Skipping draft file: {FilePath}", filePath);
                    continue;
                }

                // Check for Future date status
                if (parseResult.FrontMatter.Date.HasValue && parseResult.FrontMatter.Date.Value > DateTimeOffset.UtcNow && !config.BuildFutureDated) {
                    skippedFutureCount++;
                    _logger.LogDebug("Skipping future-dated file: {FilePath} (Date: {Date})", filePath, parseResult.FrontMatter.Date.Value);
                    continue;
                }

                // TODO: Instantiate a dedicated ContentProcessor service?
                // Determine if Post or Page (e.g., based on path or frontmatter flag)
                // For now, simple logic: if path starts with content/posts (or similar), it's a post.
                bool isPost = IsPost(filePath, config); // Implement IsPost logic

                ContentItem item;
                if (isPost) {
                    var post = new PostData();
                    // Populate PostData-specific fields
                    post.Date = parseResult.FrontMatter.Date ?? DateTimeOffset.UtcNow.DateTime; // Default to now if missing? Or throw? Require date for posts.
                    // TODO: post.ReadingTimeMinutes = _readingTimeCalculator.Calculate(parseResult.HtmlContent);
                    // TODO: post.Summary = GenerateSummary(parseResult.HtmlContent, parseResult.FrontMatter);
                    item = post;
                }
                else {
                    item = new PageData();
                    // Populate PageData-specific fields if any
                }

                // Populate common ContentItem fields
                item.SourcePath = filePath;
                item.FrontMatter = parseResult.FrontMatter;
                item.HtmlContent = parseResult.HtmlContent;
                item.SiteContext = siteContext; // Link back to context

                // Calculate Output Path and URL (critical!)
                item.Url = CalculateUrl(item, config);
                item.DestinationPath = CalculateDestinationPath(item, config);                

                // Add to context lists
                if (item is PostData postItem) {
                    if (!parseResult.FrontMatter.Date.HasValue) {
                        _logger.LogError("Post file {FilePath} is missing required 'Date' field in frontmatter. Skipping.", filePath);
                        continue; // Skip posts without dates
                    }
                    postItem.Date = parseResult.FrontMatter.Date.Value;

                    // Generate Summary if not provided in frontmatter
                    postItem.Summary = parseResult.FrontMatter.Summary?.Trim()
                                     ?? _metadataExtractor.GenerateSummary(postItem.HtmlContent, 250) // Use extractor
                                     ?? string.Empty; // Ensure Summary is not null

                    siteContext.Posts.Add(postItem);
                }
                else if (item is PageData pageItem) {
                    siteContext.Pages.Add(pageItem);
                }
                else {
                    siteContext.OtherContent.Add(item);
                }
                processedCount++;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Failed to process content file: {FilePath}", filePath);
                // Optionally continue to next file or re-throw depending on desired robustness
            }
        }
        _logger.LogInformation("Content processing complete. Processed: {ProcessedCount}, Skipped Drafts: {SkippedDrafts}, Skipped Future: {SkippedFuture}",
                            processedCount, skippedDraftCount, skippedFutureCount);
    }

    private bool IsPost(string relativePath, SiteConfiguration config) {
        // Example logic: posts are directly under a "posts" subdirectory within the content dir
        // Normalize paths for comparison
        string contentDir = config.ContentDirectory.Trim('/') + "/";
        string postsDir = config.ContentDirectory.Trim('/') + "/posts/"; // Assuming a 'posts' subfolder
        string normalizedPath = relativePath.Replace('\\', '/');

        return normalizedPath.StartsWith(postsDir, StringComparison.OrdinalIgnoreCase);
        // More robust logic might check frontmatter type: "type: post"
    }

    private string CalculateDestinationPath(ContentItem item, SiteConfiguration config) {
        // Logic to determine the output file path (e.g., _site/blog/2024/my-post/index.html)
        // Uses permalink structure defined in config.
        string urlPath = item.Url; // Use the already calculated URL

        // Convert URL path to file path
        string filePath;
        if (urlPath == "/") {
            // Root URL maps directly to index.html at the output root
            filePath = "index.html";
        }
        else {
            // Ensure it ends with a slash for 'pretty URLs' (index.html)
            if (!urlPath.EndsWith("/")) {
                urlPath += "/";
            }
            // Standard case: /some/path/ -> some/path/index.html
            filePath = urlPath.TrimStart('/') + "index.html";
        }

        _logger.LogTrace("Calculated destination path for {SourcePath} -> {DestinationPath}", item.SourcePath, filePath);
        return filePath;
    }

    private string CalculateUrl(ContentItem item, SiteConfiguration config) {
        // Logic to determine the final URL path (e.g., /blog/2024/my-post/)
        // Uses permalink structure defined in config.
        string permalinkTemplate = (item is PostData) ? config.PostPermalink : config.PagePermalink;
        string urlPath = GenerateUrlPathFromPermalink(item, permalinkTemplate, config); // Use helper

        _logger.LogTrace("Calculated URL for {SourcePath} -> {Url}", item.SourcePath, urlPath);
        return urlPath;
    }

    private string GenerateUrlPathFromPermalink(ContentItem item, string permalinkTemplate, SiteConfiguration config) {
        // Handle explicit URL override from frontmatter first
        if (!string.IsNullOrWhiteSpace(item.FrontMatter.Url)) {
            // Ensure it starts and ends with '/'
            string fmUrl = item.FrontMatter.Url.Trim();
            if (!fmUrl.StartsWith("/")) fmUrl = "/" + fmUrl;
            if (!fmUrl.EndsWith("/")) fmUrl += "/";
            return fmUrl;
        }

        // --- Handle Root index.md ---
        // Get filename relative to content directory
        string relativeToContent = item.SourcePath;
        if (relativeToContent.StartsWith(config.ContentDirectory.Trim('/'), StringComparison.OrdinalIgnoreCase)) {
            relativeToContent = relativeToContent.Substring(config.ContentDirectory.Length).TrimStart('/');
        }

        // Check if it's THE index.md at the root of the content directory
        if (relativeToContent.Equals("index.md", StringComparison.OrdinalIgnoreCase)) {
            _logger.LogTrace("Handling root index file {SourcePath}, mapping to '/'", item.SourcePath);
            return "/"; // Root index file maps to site root URL
        }

        // --- Generate Slug (only if not root index.md) ---
        string slug = item.FrontMatter.Slug?.Trim();
        if (string.IsNullOrWhiteSpace(slug)) {
            // Use title or filename (excluding index) as fallback
            string filenameWithoutExt = Path.GetFileNameWithoutExtension(item.SourcePath);
            string baseName = !string.IsNullOrWhiteSpace(item.FrontMatter.Title)
               ? item.FrontMatter.Title
               // Avoid using 'index' as the slug if filename is index.md unless title dictates it
               : (filenameWithoutExt.Equals("index", StringComparison.OrdinalIgnoreCase) ? Path.GetFileName(Path.GetDirectoryName(item.SourcePath)) ?? "untitled" : filenameWithoutExt); // Use parent dir name if index?

            slug = StringUtils.Slugify(baseName);
            _logger.LogTrace("Generated slug '{Slug}' from base '{BaseName}' for {SourcePath}", slug, baseName, item.SourcePath);
        }
        else {
            slug = StringUtils.Slugify(slug); // Ensure provided slug is clean
            _logger.LogTrace("Using slug '{Slug}' from FrontMatter for {SourcePath}", slug, item.SourcePath);
        }

        // --- Apply Permalink Template ---
        string urlPath = permalinkTemplate;
        // Check if the template contains :slug or :title placeholder
        bool needsSlug = urlPath.Contains(":slug", StringComparison.OrdinalIgnoreCase) || urlPath.Contains(":title", StringComparison.OrdinalIgnoreCase);

        if (!needsSlug) {
            // If permalink doesn't use slug (e.g., just hardcoded path), don't slugify title/filename
            _logger.LogWarning("Permalink template '{Permalink}' for {SourcePath} does not contain :slug or :title. Using template directly.", permalinkTemplate, item.SourcePath);
            // Maybe just return the cleaned template path? Requires careful thought. Let slug logic proceed for now.
        }

        // Replace placeholders in permalink template
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ":slug", slug },
            { ":title", slug } // Often :title is used interchangeably with :slug
        };

        // Add date parts if it's a PostData with a date
        if (item is PostData post && post.Date != default) {
            replacements.Add(":year", post.Date.ToString("yyyy"));
            replacements.Add(":month", post.Date.ToString("MM"));
            replacements.Add(":day", post.Date.ToString("dd"));
            // Add other date formats if needed (:i_month, :i_day)
        }

        foreach (var kvp in replacements) {
            urlPath = urlPath.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
        }

        // Basic cleanup: ensure leading slash, remove duplicate slashes
        urlPath = "/" + urlPath.Replace('\\', '/').Trim('/');
        urlPath = Regex.Replace(urlPath, "/{2,}", "/");

        // Ensure trailing slash for pretty URLs
        if (!urlPath.EndsWith("/") && !Path.HasExtension(urlPath)) // Add trailing slash unless it looks like a file path
        {
            urlPath += "/";
        }

        _logger.LogTrace("Final calculated URL path for {SourcePath}: {Url}", item.SourcePath, urlPath);

        return urlPath;
    }
        
    private void PerformPostProcessing(SiteContext siteContext) {
        _logger.LogDebug("Starting post-processing...");

        // 1. Sort Posts by Date Descending (most common)
        siteContext.Posts.Sort((a, b) => b.Date.CompareTo(a.Date));
        _logger.LogDebug("Sorted {PostCount} posts by date descending.", siteContext.Posts.Count);

        // 2. Populate Next/Previous Post properties
        PostData? previous = null;
        for (int i = siteContext.Posts.Count - 1; i >= 0; i--) // Iterate backwards to set previous easily
        {
            var current = siteContext.Posts[i];
            current.PreviousPost = previous;
            if (previous != null) {
                previous.NextPost = current;
            }
            previous = current;
        }
        _logger.LogDebug("Linked next/previous post properties.");


        // 3. Populate Taxonomies Dictionary
        siteContext.Taxonomies.Clear(); // Ensure clean state
        var categoriesMap = new Dictionary<string, List<PostData>>(StringComparer.OrdinalIgnoreCase);
        var tagsMap = new Dictionary<string, List<PostData>>(StringComparer.OrdinalIgnoreCase);

        foreach (var post in siteContext.Posts) {
            // Process Categories
            if (post.FrontMatter.Categories != null) {
                foreach (var categoryName in post.FrontMatter.Categories.Where(c => !string.IsNullOrWhiteSpace(c))) {
                    string cleanCategory = categoryName.Trim();
                    if (!categoriesMap.TryGetValue(cleanCategory, out var postsInCategory)) {
                        postsInCategory = new List<PostData>();
                        categoriesMap[cleanCategory] = postsInCategory;
                    }
                    postsInCategory.Add(post);
                }
            }

            // Process Tags
            if (post.FrontMatter.Tags != null) {
                foreach (var tagName in post.FrontMatter.Tags.Where(t => !string.IsNullOrWhiteSpace(t))) {
                    string cleanTag = tagName.Trim();
                    if (!tagsMap.TryGetValue(cleanTag, out var postsWithTag)) {
                        postsWithTag = new List<PostData>();
                        tagsMap[cleanTag] = postsWithTag;
                    }
                    postsWithTag.Add(post);
                }
            }
        }

        // Add processed maps to SiteContext (Use standard keys)
        if (categoriesMap.Any()) {
            siteContext.Taxonomies[SiteConstants.TaxonomyCategory] = categoriesMap; // Use Constant
            _logger.LogDebug("Processed {CategoryCount} categories.", categoriesMap.Count);
        }
        if (tagsMap.Any()) {
            siteContext.Taxonomies[SiteConstants.TaxonomyTag] = tagsMap; // Use Constant
            _logger.LogDebug("Processed {TagCount} tags.", tagsMap.Count);
        }

        _logger.LogInformation("Post-processing complete."); // Changed level
    }

    private async Task RenderContentItemsAsync<T>(IEnumerable<T> items, SiteContext siteContext, IFileSystem outputFileSystem, CancellationToken cancellationToken) where T : ContentItem {
        if (!items.Any()) return;

        _logger.LogDebug("Rendering {ItemCount} items of type {TypeName}...", items.Count(), typeof(T).Name);
        int successCount = 0;
        int errorCount = 0;
        var config = siteContext.Configuration;

        // Get base path for templates relative to site source root
        string templateBaseDir = config.TemplateDirectory.Trim('/');

        foreach (var item in items) {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogTrace("Rendering item: {SourcePath} -> {DestinationPath}", item.SourcePath, item.DestinationPath);

            try {
                // --- Determine Layout ---
                string? layoutName = item.FrontMatter.Layout?.Trim(); // Layout specified in frontmatter

                if (string.IsNullOrWhiteSpace(layoutName)) {
                    // No layout in frontmatter, determine default based on type
                    layoutName = (item is PostData) ? SiteConstants.DefaultPostLayout : SiteConstants.DefaultPageLayout;
                    _logger.LogTrace("No layout in frontmatter for {SourcePath}, using default '{LayoutName}'.", item.SourcePath, layoutName);
                }
                else {
                    // Ensure extension if user only provided name (e.g., "post")
                    if (!Path.HasExtension(layoutName)) {
                        layoutName += ".html"; // Assume .html extension
                    }
                    _logger.LogTrace("Using layout '{LayoutName}' from frontmatter for {SourcePath}.", layoutName, item.SourcePath);
                }

                // The key for the template engine cache is the path relative to the template directory root
                // For now, assume layouts are directly under TemplateDirectory
                string layoutCacheKey = layoutName.TrimStart('/');

                _logger.LogDebug("Attempting to render {SourcePath} with layout template key '{LayoutKey}'", item.SourcePath, layoutCacheKey);

                // --- Render ---
                // The ITemplateEngine expects the cache key (path relative to template root)
                string renderedHtml = await _templateEngine.RenderAsync(layoutCacheKey, item, cancellationToken);

                // --- Write ---
                await outputFileSystem.WriteAllTextAsync(item.DestinationPath, renderedHtml, cancellationToken);
                successCount++;
            }
            catch (FileNotFoundException fnfEx) // Catch specific error if layout template wasn't found/loaded
            {
                errorCount++;
                _logger.LogError(fnfEx, "Layout template not found for item: {SourcePath}. Attempted layout key: '{LayoutKey}'. Check template exists and was loaded.", item.SourcePath, fnfEx.Message); // fnfEx.Message might contain the key
                                                                                                                                                                                                         // Continue with next item
            }
            catch (Exception ex) {
                errorCount++;
                _logger.LogError(ex, "Failed to render or write item: {SourcePath} -> {DestinationPath}", item.SourcePath, item.DestinationPath);
                // Continue with next item
            }
        }
        _logger.LogDebug("Finished rendering {TypeName}. Success: {SuccessCount}, Errors: {ErrorCount}", typeof(T).Name, successCount, errorCount);
    }

    private async Task CompileAndWriteCssAsync(SiteConfiguration config, IFileSystem sourceFileSystem, IFileSystem outputFileSystem, CancellationToken cancellationToken) {
        string stylesDir = config.StylesDirectory;
        string entryPointFile = config.StyleEntryPoint;
        string sourcePath = sourceFileSystem.CombinePath(stylesDir, entryPointFile); // Use CombinePath

        if (!await sourceFileSystem.FileExistsAsync(sourcePath, cancellationToken)) {
            _logger.LogWarning("SCSS entry point not found: {SourcePath}. Skipping CSS compilation.", sourcePath);
            return;
        }

        _logger.LogDebug("Compiling CSS from entry point: {SourcePath}", sourcePath);
        try {
            string scssContent = await sourceFileSystem.ReadAllTextAsync(sourcePath, cancellationToken);

            // Include paths - typically the directory containing the entry point
            //var includePaths = new List<string> { Path.GetDirectoryName(sourcePath) ?? string.Empty }; // Use Path.GetDirectoryName on the full path if needed, or just pass the stylesDir relative path

            // Use stylesDir directly as include path relative to source root
            //includePaths = new List<string> { config.StylesDirectory };

            var includePaths = new List<string> { sourceFileSystem.CombinePath(sourceFileSystem.RootPath, stylesDir) };

            // Determine output style from config? Add to SiteConfiguration later.
            var outputStyle = CssOutputStyle.Compressed; // Default for production

            string? compiledCss = await _cssCompiler.CompileAsync(scssContent, sourcePath, includePaths, outputStyle, cancellationToken);

            if (compiledCss != null) {
                // Determine output path
                string outputFileName = Path.ChangeExtension(entryPointFile, ".css");
                string outputPath = outputFileSystem.CombinePath("css", outputFileName); // Place in a 'css' subfolder in output

                _logger.LogDebug("Writing compiled CSS to: {OutputPath}", outputPath);
                await outputFileSystem.WriteAllTextAsync(outputPath, compiledCss, cancellationToken);
                _logger.LogInformation("CSS compiled successfully.");
            }
            else {
                _logger.LogError("CSS compilation failed. See previous errors.");
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error during CSS compilation process for {SourcePath}", sourcePath);
        }
    }

    private async Task CopyStaticFilesAsync(string staticDir, IFileSystem sourceFileSystem, IFileSystem outputFileSystem, CancellationToken cancellationToken) {
        if (!await sourceFileSystem.DirectoryExistsAsync(staticDir, cancellationToken)) {
            _logger.LogDebug("Static files directory not found: {StaticDir}. Nothing to copy.", staticDir);
            return;
        }

        IEnumerable<string> staticFiles;
        try {
            staticFiles = await sourceFileSystem.GetFilesAsync(staticDir, "*.*", true, cancellationToken);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error discovering static files in {StaticDir}", staticDir);
            return;
        }

        int copiedCount = 0;
        int errorCount = 0;
        _logger.LogDebug("Found {FileCount} static files/directories to copy.", staticFiles.Count());

        foreach (var sourceRelativePath in staticFiles) {
            cancellationToken.ThrowIfCancellationRequested();

            // Determine destination path relative to output root
            // Need path relative to the staticDir itself
            string pathInsideStaticDir = sourceRelativePath.Substring(staticDir.Length).TrimStart('/');
            string destRelativePath = pathInsideStaticDir; // Output path matches structure inside static dir

            _logger.LogTrace("Copying static file: {SourcePath} -> {DestinationPath}", sourceRelativePath, destRelativePath);

            try {
                // Read from source, write to destination
                // Using streams might be more memory efficient for large files
                using (var stream = await sourceFileSystem.OpenReadStreamAsync(sourceRelativePath, cancellationToken)) {
                    await outputFileSystem.WriteStreamAsync(destRelativePath, stream, cancellationToken);
                }

                // Alternatively, use CopyFileAsync if implemented efficiently by IFileSystem adapters
                // await sourceFileSystem.CopyFileAsync(sourceRelativePath, destRelativePath, true, cancellationToken); // Requires CopyFileAsync in IFileSystem and implementation

                copiedCount++;
            }
            catch (Exception ex) {
                errorCount++;
                _logger.LogError(ex, "Failed to copy static file: {SourcePath} -> {DestinationPath}", sourceRelativePath, destRelativePath);
                // Continue with next file
            }
        }
        _logger.LogInformation("Static file copying complete. Copied: {CopiedCount}, Errors: {ErrorCount}", copiedCount, errorCount);
    }

    private Task GenerateListPagesAsync(SiteContext siteContext, CancellationToken cancellationToken) {
        _logger.LogInformation("Generating taxonomy listing pages...");
        var config = siteContext.Configuration;
        int generatedCount = 0;

        foreach (var taxonomyPair in siteContext.Taxonomies) {
            cancellationToken.ThrowIfCancellationRequested();
            string taxonomyType = taxonomyPair.Key; // e.g., "category", "tag"
            var terms = taxonomyPair.Value; // Dictionary<string, List<PostData>>

            string basePath;
            string listType;
            string defaultLayout;

            // Determine base path and type based on taxonomy key
            if (taxonomyType.Equals(SiteConstants.TaxonomyCategory, StringComparison.OrdinalIgnoreCase)) {
                basePath = config.CategoryUrlBasePath?.Trim('/') ?? SiteConstants.TaxonomyCategory;
                listType = "Category";
                defaultLayout = SiteConstants.DefaultTaxonomyLayout; // e.g., "taxonomy.html" or "list.html"
            }
            else if (taxonomyType.Equals(SiteConstants.TaxonomyTag, StringComparison.OrdinalIgnoreCase)) {
                basePath = config.TagUrlBasePath?.Trim('/') ?? SiteConstants.TaxonomyTag;
                listType = "Tag";
                defaultLayout = SiteConstants.DefaultTaxonomyLayout;
            }
            else {
                _logger.LogWarning("Skipping unknown taxonomy type: {TaxonomyType}", taxonomyType);
                continue;
            }

            _logger.LogDebug("Generating pages for taxonomy type '{TaxonomyType}' with base path '/{BasePath}/'", taxonomyType, basePath);

            foreach (var termPair in terms) {
                cancellationToken.ThrowIfCancellationRequested();
                string termName = termPair.Key;
                List<PostData> termPosts = termPair.Value;

                if (!termPosts.Any()) continue; // Skip terms with no posts

                // Generate slug for URL/Path
                string termSlug = StringUtils.Slugify(termName);

                // Create ListPageData instance
                var listPage = new ListPageData {
                    ListType = listType,
                    Term = termName,
                    TermSlug = termSlug,
                    Posts = termPosts.OrderByDescending(p => p.Date).ToList(), // Ensure posts are sorted
                    SiteContext = siteContext,
                    // Generate Title for the page
                    FrontMatter = new FrontMatter {
                        // Use term name for title, layout from constants
                        Title = $"{listType}: {termName}",
                        Layout = defaultLayout
                        // Add other frontmatter defaults if needed (e.g., sitemap priority/freq for lists)
                    },
                    // Set HtmlContent to empty - layout is responsible for displaying list
                    HtmlContent = string.Empty,
                    // Calculate URL and DestinationPath
                    Url = $"/{basePath}/{termSlug}/",
                    DestinationPath = $"{basePath}/{termSlug}/index.html",
                    // Set SourcePath conceptually (doesn't exist on disk)
                    SourcePath = $"_generated/{taxonomyType}/{termSlug}.md" // Conceptual path
                };

                // Recalculate URL/Dest based on FrontMatter.Url override if needed? Usually not for generated pages.

                siteContext.ListPages.Add(listPage);
                generatedCount++;
                _logger.LogTrace("Generated list page for {ListType} '{Term}' at URL {Url}", listType, termName, listPage.Url);
            }
        }

        _logger.LogInformation("Generated {Count} taxonomy list pages.", generatedCount);


        // --- Generate Blog Index Page ---
        int blogIndexPagesGenerated = 0;
        if (siteContext.Posts.Any()) // Only generate if there are posts
        {
            _logger.LogDebug("Generating blog index page(s)...");
            // TODO: Implement Pagination Here for the main blog list later (#2)
            // For now, just create the first page with all posts (or limited by config?)

            var postsForBlogIndex = siteContext.Posts; // Use all posts for now
            string blogIndexUrl = "/blog/"; // Define the URL for the main blog index
            string blogIndexDestPath = "blog/index.html"; // Relative output path

            var blogIndexPage = new ListPageData {
                ListType = "BlogIndex",
                Term = "Main", // Conceptual term
                TermSlug = "blog", // Conceptual slug
                Posts = postsForBlogIndex, // Assign ALL posts for now
                SiteContext = siteContext,
                FrontMatter = new FrontMatter {
                    Title = "Blog Archive", // Or get from config?
                    Layout = SiteConstants.DefaultListLayout // Use the standard list layout
                },
                HtmlContent = string.Empty,
                Url = blogIndexUrl,
                DestinationPath = blogIndexDestPath,
                SourcePath = "_generated/blog/index.md" // Conceptual path
            };

            siteContext.ListPages.Add(blogIndexPage);
            blogIndexPagesGenerated++;
            _logger.LogTrace("Generated main blog index page at URL {Url}", blogIndexPage.Url);

        }
        else {
            _logger.LogInformation("No posts found, skipping blog index page generation.");
        }

        _logger.LogInformation("Generated {Count} blog index page(s).", blogIndexPagesGenerated);
        // Update total count
        generatedCount += blogIndexPagesGenerated;
        _logger.LogInformation("Total list pages generated: {Count}", generatedCount);

        return Task.CompletedTask; // Generation is synchronous for now
    }
}
