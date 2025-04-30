using System.Text.Json;
using System.Text.RegularExpressions;

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
    private readonly MarkdigContentParser _markdownParser;
    private readonly HtmlContentParser _htmlParser;
    private readonly ITemplateEngine _templateEngine;
    private readonly ICssCompiler _cssCompiler;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly PluginManager _pluginManager;
    // Add other services as needed: IMetadataExtractor, IReadingTimeCalculator, PluginManager etc.

    public SiteBuilder(
        ILogger<SiteBuilder> logger,
        MarkdigContentParser markdownParser,
        HtmlContentParser htmlParser,
        ITemplateEngine templateEngine,
        ICssCompiler cssCompiler,
        IMetadataExtractor metadataExtractor,
        PluginManager pluginManager) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _markdownParser = markdownParser ?? throw new ArgumentNullException(nameof(markdownParser));
        _htmlParser = htmlParser ?? throw new ArgumentNullException(nameof(htmlParser));
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
            _logger.LogInformation("--- Build Step 1: Load Configuration --- from: {ConfigPath}", configPath);
            configuration = await LoadConfigurationAsync(configPath, sourceFileSystem, cancellationToken);
            if (configuration == null) {
                _logger.LogCritical("Site configuration could not be loaded. Aborting build.");
                return;
            }
            ValidateConfiguration(configuration);

            // Initialize Site Context (after config loaded)
            siteContext = new SiteContext(configuration);

            _logger.LogInformation("--- Build Step 2: Run PreBuild Plugins ---");
            await _pluginManager.RunPluginsAsync(PipelineStage.PreBuild, siteContext, sourceFileSystem, outputFileSystem, cancellationToken);

            _logger.LogInformation("--- Build Step 3: Initialize Output Directory ---");
            await outputFileSystem.CreateDirectoryAsync(string.Empty, cancellationToken);

            _logger.LogInformation("--- Build Step 4: Initialize Template Engine ---");
            await _templateEngine.InitializeAsync(configuration, sourceFileSystem, cancellationToken);

            _logger.LogInformation("--- Build Step 5: Process Content Files --- from: {ContentDirectory}", configuration.ContentDirectory);
            await ProcessContentFilesAsync(siteContext, sourceFileSystem, cancellationToken);
            _logger.LogDebug("Counts after ProcessContentFilesAsync - Posts: {P}, Pages: {A}, Other: {O}", siteContext.Posts.Count, siteContext.Pages.Count, siteContext.OtherContent.Count);

            _logger.LogInformation("--- Build Step 6: Run PostContentProcessing Plugins ---");
            await _pluginManager.RunPluginsAsync(PipelineStage.PostContentProcessing, siteContext, sourceFileSystem, outputFileSystem, cancellationToken);
            _logger.LogDebug("Counts after PostContentProcessing Plugins - Posts: {P}, Pages: {A}, Other: {O}", siteContext.Posts.Count, siteContext.Pages.Count, siteContext.OtherContent.Count);

            // Post-Processing (Sorting, Relationships, Taxonomies, Pagination)
            _logger.LogInformation("--- Build Step 7: Perform Post-Processing ---");
            PerformPostProcessing(siteContext);
            _logger.LogDebug("Counts after PerformPostProcessing - Posts: {P}, Pages: {A}, Taxonomies: {T}", siteContext.Posts.Count, siteContext.Pages.Count, siteContext.Taxonomies.Count);

            _logger.LogInformation("--- Build Step 8: Generate List Pages ---");
            await GenerateListPagesAsync(siteContext, cancellationToken); // Creates ListPages from Taxonomies + Blog Index
            _logger.LogDebug("Counts after GenerateListPagesAsync - ListPages: {L}", siteContext.ListPages.Count);

            // TODO: 9. Process Paginated Lists (Generate list pages)
            // await ProcessPaginationAsync(siteContext, outputFileSystem, cancellationToken);

            // Render Content Pages
            _logger.LogInformation("--- Build Step 10: Render Content Pages ---");
            _logger.LogDebug("Attempting to render {Count} Posts...", siteContext.Posts.Count);
            await RenderContentItemsAsync(siteContext.Posts, siteContext, outputFileSystem, cancellationToken);
            _logger.LogDebug("Attempting to render {Count} Pages...", siteContext.Pages.Count);
            await RenderContentItemsAsync(siteContext.Pages, siteContext, outputFileSystem, cancellationToken);
            _logger.LogDebug("Attempting to render {Count} ListPages...", siteContext.ListPages.Count);
            await RenderContentItemsAsync(siteContext.ListPages, siteContext, outputFileSystem, cancellationToken);

            // TODO: Run PostRender Plugins here if implemented (needs careful thought on data flow)
            _logger.LogInformation("--- Build Step 11: Run PostRender Plugins ---");
            await _pluginManager.RunPluginsAsync(PipelineStage.PostRender, siteContext, sourceFileSystem, outputFileSystem, cancellationToken);

            // Compile CSS
            _logger.LogInformation("--- Build Step 11: Compile CSS ---");
            await CompileAndWriteCssAsync(configuration, sourceFileSystem, outputFileSystem, cancellationToken);

            // Copy Static Files
            _logger.LogInformation("--- Build Step 12: Copy Static Files --- from: {StaticDirectory}", configuration.StaticDirectory);
            await CopyStaticFilesAsync(configuration.StaticDirectory, sourceFileSystem, outputFileSystem, cancellationToken);

            // --- Run PostBuild Plugins ---
            // Run AFTER essential files (content, css, static) are written
            _logger.LogInformation("--- Build Step 13: Run PostBuild Plugins ---");
            await _pluginManager.RunPluginsAsync(PipelineStage.PostBuild, siteContext, sourceFileSystem, outputFileSystem, cancellationToken);
            
            // --- Run BuildComplete Plugins --- (Run even before final success log)
            _logger.LogInformation("--- Build Step 14: Run BuildComplete Plugins ---");
            await _pluginManager.RunPluginsAsync(PipelineStage.BuildComplete, siteContext, sourceFileSystem, outputFileSystem, cancellationToken);

            stopwatch.Stop();
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
            // Find all markdown AND html files recursively within the content directory
            var mdFiles = await sourceFileSystem.GetFilesAsync(config.ContentDirectory, "*.md", true, cancellationToken);
            var htmlFiles = await sourceFileSystem.GetFilesAsync(config.ContentDirectory, "*.html", true, cancellationToken);
            var htmFiles = await sourceFileSystem.GetFilesAsync(config.ContentDirectory, "*.htm", true, cancellationToken);

            contentFiles = mdFiles.Concat(htmlFiles).Concat(htmFiles).Distinct(); // Combine lists

            if (!contentFiles.Any()) {
                _logger.LogInformation("No .md, .html, or .htm files found in {ContentDirectory}.", config.ContentDirectory);
                return;
            }
            _logger.LogDebug("Found {Count} potential content files (.md, .html, .htm) in {ContentDirectory}", contentFiles.Count(), config.ContentDirectory);
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

                // *** Change: Select parser based on extension ***
                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                IContentParser selectedParser;

                if (extension == ".md") {
                    selectedParser = _markdownParser;
                    _logger.LogTrace("Using Markdown parser for {FilePath}", filePath);
                }
                else if (extension == ".html" || extension == ".htm") {
                    selectedParser = _htmlParser;
                    _logger.LogTrace("Using HTML parser for {FilePath}", filePath);
                }
                else {
                    _logger.LogWarning("Skipping file with unsupported content extension: {FilePath}", filePath);
                    continue; // Skip files that aren't md or html/htm
                }

                // Use the selected parser
                var parseResult = await selectedParser.ParseAsync(rawContent, filePath, cancellationToken);
                // *** End Change ***


                // --- Existing Logic (Checks, Type Determination, Population) ---
                // Check for Draft status
                if (parseResult.FrontMatter.Draft && !config.BuildDrafts) {
                    skippedDraftCount++;
                    _logger.LogDebug("Skipping draft file: {FilePath}", filePath);
                    continue;
                }

                // Check for Future date status
                if (parseResult.FrontMatter.Date.HasValue && parseResult.FrontMatter.Date.Value > DateTime.UtcNow && !config.BuildFutureDated) {
                    skippedFutureCount++;
                    _logger.LogDebug("Skipping future-dated file: {FilePath} (Date: {Date})", filePath, parseResult.FrontMatter.Date.Value);
                    continue;
                }

                // Determine if Post or Page
                bool isPost = IsPost(filePath, config);

                ContentItem item;
                if (isPost) {
                    var post = new PostData();
                    // Populate PostData-specific fields
                    if (!parseResult.FrontMatter.Date.HasValue) {
                        _logger.LogError("Post file {FilePath} is missing required 'Date' field in frontmatter. Skipping.", filePath);
                        continue; // Skip posts without dates
                    }
                    post.Date = parseResult.FrontMatter.Date.Value; // Non-nullable Date on PostData requires value
                    item = post;
                }
                else {
                    item = new PageData();
                    // Populate PageData-specific fields if any
                }

                // Populate common ContentItem fields
                item.SourcePath = filePath;
                item.FrontMatter = parseResult.FrontMatter;
                item.HtmlContent = parseResult.HtmlContent; // This is Markdown->HTML OR Original HTML body
                item.SiteContext = siteContext;

                // Calculate Output Path and URL
                item.Url = CalculateUrl(item, config);
                item.DestinationPath = CalculateDestinationPath(item, config);

                // Add to context lists (and generate summary for posts)
                if (item is PostData postItem) {
                    // Generate Summary if not provided in frontmatter (applies to both MD and HTML posts)
                    postItem.Summary = parseResult.FrontMatter.Summary?.Trim()
                                     // Generate summary from HtmlContent (which is already HTML)
                                     ?? _metadataExtractor.GenerateSummary(postItem.HtmlContent, 250)
                                     ?? string.Empty;

                    siteContext.Posts.Add(postItem);
                }
                else if (item is PageData pageItem) {
                    siteContext.Pages.Add(pageItem);
                }
                else // Should not happen with current logic, but keep for safety
                {
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
        // Check if it's a draft being built
        bool isDraftBuilt = item.FrontMatter.Draft && config.BuildDrafts;

        string relativeOutputPath;

        if (isDraftBuilt) {
            // --- DRAFT PATH LOGIC ---
            _logger.LogDebug("Calculating DRAFT destination path for {SourcePath}", item.SourcePath);
            // Strategy: Mirror source path structure relative to content root, place under 'drafts' output folder.

            // 1. Get path relative to content directory
            string pathRelativeToContent = item.SourcePath;
            string contentDirPrefix = config.ContentDirectory.TrimEnd('/') + "/"; // Ensure trailing slash for prefix check
            if (pathRelativeToContent.StartsWith(contentDirPrefix, StringComparison.OrdinalIgnoreCase)) {
                pathRelativeToContent = pathRelativeToContent.Substring(contentDirPrefix.Length);
            }
            // Example: "posts/subdir/draft-post.md" or "pages/draft-page.md" or "index.md"

            // 2. Determine output directory structure based on relative source path
            string dirName = Path.GetDirectoryName(pathRelativeToContent)?.Replace('\\', '/') ?? string.Empty;
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(item.SourcePath);

            // 3. Construct path: Use directory structure, use slug for filename unless original was index.md
            string finalDirPart;
            if (fileNameWithoutExt.Equals("index", StringComparison.OrdinalIgnoreCase)) {
                // If the source was index.md, output an index.html in the corresponding directory
                finalDirPart = dirName; // Output to the directory itself (e.g., posts/subdir/index.html relative part)
            }
            else {
                // Use slug for non-index files
                string slug = item.FrontMatter.Slug?.Trim();
                if (string.IsNullOrWhiteSpace(slug)) {
                    // Use Title or filename as fallback for slug basis
                    string baseName = !string.IsNullOrWhiteSpace(item.FrontMatter.Title)
                      ? item.FrontMatter.Title
                      : fileNameWithoutExt; // Use filename if no title/slug
                    slug = StringUtils.Slugify(baseName);
                }
                else {
                    slug = StringUtils.Slugify(slug); // Ensure provided slug is clean
                }
                // Combine directory and slug
                finalDirPart = string.IsNullOrEmpty(dirName) ? slug : $"{dirName}/{slug}";
            }

            // Ensure output path ends with /index.html for directory-based output
            relativeOutputPath = $"{finalDirPart}/index.html";
            // Clean potential double slashes (e.g., if finalDirPart was empty)
            relativeOutputPath = relativeOutputPath.Replace("//", "/").TrimStart('/');

            _logger.LogTrace("Draft destination path: {Path}", relativeOutputPath);
        }
        else {
            // --- REGULAR PATH LOGIC (Unchanged) ---
            _logger.LogDebug("Calculating regular destination path for {SourcePath}", item.SourcePath);
            // Original logic: Convert URL path to file path
            string urlPath = item.Url; // Use the already calculated URL

            if (string.IsNullOrWhiteSpace(urlPath)) {
                // Fallback if URL is somehow empty - prevent crashing
                _logger.LogWarning("URL for item {SourcePath} was empty. Using fallback destination.", item.SourcePath);
                relativeOutputPath = $"undetermined/{StringUtils.Slugify(Path.GetFileNameWithoutExtension(item.SourcePath))}/index.html";
            }
            else if (urlPath == "/") {
                relativeOutputPath = "index.html";
            }
            else {
                // Ensure it ends with a slash for 'pretty URLs' (index.html)
                if (!urlPath.EndsWith("/")) {
                    urlPath += "/";
                }
                // Standard case: /some/path/ -> some/path/index.html
                relativeOutputPath = urlPath.TrimStart('/') + "index.html";
            }
        }

        // Clean path one last time
        relativeOutputPath = relativeOutputPath.Replace("//", "/");

        _logger.LogTrace("Final destination path for {SourcePath} -> {DestinationPath}", item.SourcePath, relativeOutputPath);
        return relativeOutputPath;
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
        if (relativeToContent.Equals("index.md", StringComparison.OrdinalIgnoreCase) || relativeToContent.Equals("index.html", StringComparison.OrdinalIgnoreCase)) {
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
                _logger.LogDebug("Attempting to render item '{SourcePath}' using template cache key '{LayoutKey}'", item.SourcePath, layoutCacheKey);
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

        if (!await sourceFileSystem.DirectoryExistsAsync(stylesDir, cancellationToken)) {
            _logger.LogDebug("Styles directory not found: {StylesDir}. Skipping CSS compilation.", stylesDir);
            return;
        }

        _logger.LogInformation("--- Build Step 11: Compile SCSS Files --- from: {StylesDirectory}", stylesDir);
        IEnumerable<string> scssFilesToCompile;

        try {
            // Find all .scss files recursively within the styles directory
            // Change recursive: true if you want to compile files in subfolders too
            var allScssFiles = await sourceFileSystem.GetFilesAsync(stylesDir, "*.scss", true, cancellationToken);

            // Filter out partials (files starting with '_')
            scssFilesToCompile = allScssFiles
                .Where(f => !Path.GetFileName(f).StartsWith("_"))
                .ToList(); // Execute query

            if (!scssFilesToCompile.Any()) {
                _logger.LogInformation("No non-partial SCSS files found in {StylesDir} to compile.", stylesDir);
                return;
            }

            _logger.LogDebug("Found {Count} non-partial SCSS files to compile: [{FileNames}]",
                scssFilesToCompile.Count(), string.Join(", ", scssFilesToCompile.Select(Path.GetFileName)));
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error discovering SCSS files in {StylesDir}", stylesDir);
            return; // Stop processing if discovery fails
        }

        int successCount = 0;
        int errorCount = 0;
        string outputCssBaseDir = "css"; // Output subfolder (relative to output root)

        // Determine output style from config? Add to SiteConfiguration later.
        // Defaulting to Compressed for production builds.
        var outputStyle = CssOutputStyle.Compressed;

        foreach (var sourceRelativePath in scssFilesToCompile) {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Compiling SCSS file: {SourcePath}", sourceRelativePath);

            try {
                string scssContent = await sourceFileSystem.ReadAllTextAsync(sourceRelativePath, cancellationToken);

                // Compile the individual SCSS file
                string? compiledCss = await _cssCompiler.CompileAsync(scssContent, sourceRelativePath, sourceFileSystem, outputStyle, cancellationToken);

                if (compiledCss != null) {
                    // Determine the output path, preserving directory structure relative to stylesDir
                    // 1. Get path relative to the styles directory root
                    string pathInsideStylesDir = sourceRelativePath;
                    if (sourceRelativePath.StartsWith(stylesDir, StringComparison.OrdinalIgnoreCase)) {
                        pathInsideStylesDir = sourceRelativePath.Substring(stylesDir.Length).TrimStart('/');
                    }

                    // 2. Change the extension to .css
                    string cssFileName = Path.ChangeExtension(pathInsideStylesDir, ".css");

                    // 3. Combine with the output base CSS directory (e.g., "css")
                    string outputPath = outputFileSystem.CombinePath(outputCssBaseDir, cssFileName);

                    _logger.LogDebug("Writing compiled CSS for {SourceFile} to: {OutputPath}", Path.GetFileName(sourceRelativePath), outputPath);
                    await outputFileSystem.WriteAllTextAsync(outputPath, compiledCss, cancellationToken);
                    successCount++;
                }
                else {
                    _logger.LogError("CSS compilation failed for {SourcePath}. See previous errors.", sourceRelativePath);
                    errorCount++;
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error during CSS compilation process for {SourcePath}", sourceRelativePath);
                errorCount++;
                // Continue with the next file
            }
        }

        if (errorCount > 0) {
            _logger.LogWarning("CSS compilation finished with {ErrorCount} errors. Successfully compiled: {SuccessCount}", errorCount, successCount);
        }
        else {
            _logger.LogInformation("CSS compilation finished. Successfully compiled: {SuccessCount} files.", successCount);
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
        int totalPagesGenerated = 0;
        // Store newly generated pages here temporarily to avoid modifying collection while iterating
        var newListPages = new List<ListPageData>();

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
                List<PostData> allTermPosts = termPair.Value.OrderByDescending(p => p.Date).ToList();

                if (!allTermPosts.Any()) continue; // Skip terms with no posts

                // Generate slug for URL/Path
                string termSlug = StringUtils.Slugify(termName);

                // --- Pagination Logic for Taxonomies ---
                int postsPerPage = config.PostsPerPage <= 0 ? int.MaxValue : config.PostsPerPage; // Use config value
                int totalPosts = allTermPosts.Count;
                int totalPages = (int)Math.Ceiling((double)totalPosts / postsPerPage);

                _logger.LogDebug("Generating {TotalPages} pages for {ListType} '{Term}' ({TotalPosts} posts)...", totalPages, listType, termName, totalPosts);

                string baseUrlPath = $"/{basePath}/{termSlug}"; // Base URL for this term

                for (int currentPage = 1; currentPage <= totalPages; currentPage++) {
                    cancellationToken.ThrowIfCancellationRequested();

                    var postsForThisPage = allTermPosts
                        .Skip((currentPage - 1) * postsPerPage)
                        .Take(postsPerPage)
                        .ToList();

                    // Calculate URL and Destination Path for this specific page number
                    string pageUrl, pageDestinationPath;
                    if (currentPage == 1) {
                        pageUrl = $"{baseUrlPath}/"; // Page 1 is at the root for the term
                        pageDestinationPath = $"{basePath}/{termSlug}/index.html";
                    }
                    else {
                        // Define structure for page > 1 (e.g., /tags/csharp/page/2/)
                        pageUrl = $"{baseUrlPath}/page/{currentPage}/";
                        pageDestinationPath = $"{basePath}/{termSlug}/page/{currentPage}/index.html";
                    }

                    var listPage = new ListPageData {
                        ListType = listType,
                        Term = termName,
                        TermSlug = termSlug,
                        Posts = postsForThisPage, // Only posts for *this* page
                        SiteContext = siteContext,
                        FrontMatter = new FrontMatter {
                            Title = $"{listType}: {termName}" + (totalPages > 1 ? $" (Page {currentPage})" : ""),
                            Layout = defaultLayout
                        },
                        HtmlContent = string.Empty,
                        Url = pageUrl,
                        DestinationPath = pageDestinationPath,
                        SourcePath = $"_generated/{taxonomyType}/{termSlug}/page{currentPage}.md", // Conceptual path
                        PagerInfo = new Pager<PostData> // Populate Pager
                        {
                            ItemsOnPage = postsForThisPage, // Redundant? Posts prop already has this.
                            CurrentPage = currentPage,
                            TotalPages = totalPages,
                            TotalItems = totalPosts,
                            ItemsPerPage = postsPerPage,
                            // Calculate Prev/Next URLs
                            PreviousPageUrl = currentPage > 1 ? (currentPage == 2 ? $"{baseUrlPath}/" : $"{baseUrlPath}/page/{currentPage - 1}/") : null,
                            NextPageUrl = currentPage < totalPages ? $"{baseUrlPath}/page/{currentPage + 1}/" : null,
                            PageUrlTemplate = $"{baseUrlPath}/page/:num/", // For generating page number links
                            FirstPageUrl = $"{baseUrlPath}/"
                        }
                    };
                    newListPages.Add(listPage);
                    totalPagesGenerated++;
                } // End loop for current page
            } // End loop for terms
        } // End loop for taxonomy types
        _logger.LogInformation("Generated {Count} taxonomy list pages (across all pages).", totalPagesGenerated);
        
        // --- Generate Blog Index Page(s) ---
        int blogIndexPagesGenerated = 0;
        if (siteContext.Posts.Any()) {
            _logger.LogDebug("Generating blog index page(s)...");
            List<PostData> allBlogPosts = siteContext.Posts; // Assumes already sorted by PerformPostProcessing
            int postsPerPage = config.PostsPerPage <= 0 ? int.MaxValue : config.PostsPerPage;
            int totalPosts = allBlogPosts.Count;
            int totalPages = (int)Math.Ceiling((double)totalPosts / postsPerPage);
            string blogIndexBasePath = "/blog"; // Base path for blog index

            _logger.LogDebug("Generating {TotalPages} pages for Blog Index ({TotalPosts} posts)...", totalPages, totalPosts);

            for (int currentPage = 1; currentPage <= totalPages; currentPage++) {
                cancellationToken.ThrowIfCancellationRequested();
                var postsForThisPage = allBlogPosts
                    .Skip((currentPage - 1) * postsPerPage)
                    .Take(postsPerPage)
                    .ToList();

                string pageUrl, pageDestinationPath;
                if (currentPage == 1) {
                    pageUrl = $"{blogIndexBasePath}/";
                    pageDestinationPath = "blog/index.html";
                }
                else {
                    pageUrl = $"{blogIndexBasePath}/page/{currentPage}/";
                    pageDestinationPath = $"blog/page/{currentPage}/index.html";
                }

                var blogIndexPage = new ListPageData {
                    ListType = "BlogIndex",
                    Term = "Main",
                    TermSlug = "blog", // Conceptual
                    Posts = postsForThisPage,
                    SiteContext = siteContext,
                    FrontMatter = new FrontMatter {
                        Title = "Blog Archive" + (totalPages > 1 ? $" (Page {currentPage})" : ""),
                        Layout = SiteConstants.DefaultListLayout
                    },
                    HtmlContent = string.Empty,
                    Url = pageUrl,
                    DestinationPath = pageDestinationPath,
                    SourcePath = $"_generated/blog/page{currentPage}.md", // Conceptual
                    PagerInfo = new Pager<PostData> {
                        ItemsOnPage = postsForThisPage,
                        CurrentPage = currentPage,
                        TotalPages = totalPages,
                        TotalItems = totalPosts,
                        ItemsPerPage = postsPerPage,
                        PreviousPageUrl = currentPage > 1 ? (currentPage == 2 ? $"{blogIndexBasePath}/" : $"{blogIndexBasePath}/page/{currentPage - 1}/") : null,
                        NextPageUrl = currentPage < totalPages ? $"{blogIndexBasePath}/page/{currentPage + 1}/" : null,
                        PageUrlTemplate = $"{blogIndexBasePath}/page/:num/",
                        FirstPageUrl = $"{blogIndexBasePath}/"
                    }
                };
                newListPages.Add(blogIndexPage);
                blogIndexPagesGenerated++;
                totalPagesGenerated++;
            } // End loop for current page
        }
        else { /* log skip */
        }
        _logger.LogInformation("Generated {Count} blog index pages.", blogIndexPagesGenerated);


        // --- Add all generated pages to the main context ---
        siteContext.ListPages.AddRange(newListPages);
        _logger.LogInformation("Total list pages generated (including pagination): {Count}", totalPagesGenerated);

        return Task.CompletedTask;
    }
}
