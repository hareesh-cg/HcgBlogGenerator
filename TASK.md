# HcgBlogGenerator - Task Tracking

This document tracks the development status, ongoing tasks, backlog, and future ideas for the HcgBlogGenerator project.

## Completed Tasks

*   [x] Initial project setup (Solution, .gitignore, LICENSE, .editorconfig)
*   [x] Define project structure and architecture (`.cursorrules`, `PLANNING.md`)
*   [x] Create initial `README.md`
*   [x] Create initial `PLANNING.md`
*   [x] Create initial `TASK.md`

## Current Sprint / In Progress

*   [ ] Setup Core project (`HcgBlogGenerator.Core.csproj`)
*   [ ] Define core interfaces (`IFileSystem`, `IConfigurationLoader`, etc.) in `HcgBlogGenerator.Core/Interfaces/`
*   [ ] Define core models (`SiteConfiguration`, `ContentItem`, etc.) in `HcgBlogGenerator.Core/Models/`
*   [ ] Setup ConsoleApp project (`HcgBlogGenerator.ConsoleApp.csproj`)
*   [ ] Implement basic CLI structure using `System.CommandLine` in `HcgBlogGenerator.ConsoleApp/Program.cs`
*   [ ] Setup basic Dependency Injection in `HcgBlogGenerator.ConsoleApp/Program.cs`

## Backlog / Planned Features

### Core Functionality
*   [ ] **Initialize Command:**
    *   [ ] Implement `InitializeSiteHandler`
    *   [ ] Create boilerplate files/folders (`_layouts`, `_includes`, `_posts`, `_assets`, `_config.json`, `index.md`)
    *   [ ] Add `init` command to ConsoleApp
*   [ ] **Build Command:**
    *   [ ] Implement `BuildSiteHandler`
    *   [ ] Implement `SiteGeneratorService` orchestration logic
    *   [ ] Implement `ContentDiscoveryService` (find markdown, layouts, assets)
    *   [ ] Implement `ConfigurationLoader` (using Newtonsoft.Json)
    *   [ ] Implement `IFileSystem` using `System.IO.Abstractions` (for local testing)
    *   [ ] Add `build` command to ConsoleApp
*   [ ] **Draft Support:**
    *   [ ] Logic to identify draft posts (e.g., in `_drafts` folder or based on frontmatter)
    *   [ ] Option in `build` command to include/exclude drafts

### Content Management
*   [ ] **Markdown Parsing:**
    *   [ ] Implement `MarkdownParser` using Markdig
    *   [ ] Integrate `IMarkdownParser` into build process
*   [ ] **Front Matter Parsing:**
    *   [ ] Implement `FrontMatterParser` using YamlDotNet
    *   [ ] Integrate `IFrontMatterParser` into content discovery/processing
*   [ ] **Template Rendering:**
    *   [ ] Implement `TemplateEngine` using Scriban
    *   [ ] Implement `ITemplateRenderer` service
    *   [ ] Handle layouts and partials (`_includes`)
    *   [ ] Define `TemplateData` model structure
*   [ ] **SCSS Compilation:**
    *   [ ] Implement `ScssCompiler` using SharpScss
    *   [ ] Integrate `IScssCompiler` into asset processing pipeline
*   [ ] **Asset Handling:**
    *   [ ] Implement `IAssetProcessor` service
    *   [ ] Copy static assets to output directory
    *   [ ] Handle relative paths correctly
*   [ ] **Code Syntax Highlighting:** (Likely via Markdig extensions)
*   [ ] **Image Optimization:** (Requires external library/process integration - consider plugin?)

### Blogging Features
*   [ ] **Post Model:** Define `Post.cs` inheriting `ContentItem`
*   [ ] **Page Model:** Define `Page.cs` inheriting `ContentItem`
*   [ ] **Post Listing & Pagination:**
    *   [ ] Logic to collect and sort posts
    *   [ ] Generate paginated index pages
    *   [ ] Make pagination configurable
*   [ ] **Categories & Tags:**
    *   [ ] Read categories/tags from frontmatter
    *   [ ] Generate category/tag listing pages
*   [ ] **Reading Time Estimation:**
    *   [ ] Implement reading time calculation logic
    *   [ ] Expose reading time in `TemplateData`
*   [ ] **Related Posts:** (Requires content analysis - consider plugin?)
*   [ ] **RSS Feed Generation:** (Implement as built-in `RssPlugin`)
*   [ ] **Search Functionality:** (Requires indexing - consider plugin or separate service)

### Plugin System
*   [ ] Define `IBuildLifecyclePlugin` interface
*   [ ] Implement `PluginManager` service
*   [ ] Implement plugin discovery mechanism
*   [ ] Define `PluginContext` model

### SEO & Performance
*   [ ] **Meta Tags & Open Graph:** (Implement as built-in `SeoPlugin`)
*   [ ] **Sitemap Generation:** (Implement as built-in `SitemapPlugin`)
*   [ ] **robots.txt Generation:** (Implement as built-in `RobotsPlugin`)
*   [ ] **Performance Optimization:** (Ongoing - profiling, caching)
*   [ ] **Asset Fingerprinting:** (Append hash to asset filenames for cache busting)

### UI & Styling
*   [ ] **CSS Modules:** (Investigate integration possibilities)
*   [ ] **Styled Components:** (Likely out of scope for backend generator)

### Cloud Implementations
*   [ ] **AWS:**
    *   [ ] Setup `HcgBlogGenerator.AWS` project
    *   [ ] Implement `S3FileSystem`
    *   [ ] Implement `CloudWatchLoggerProvider`
    *   [ ] Create `LambdaHandler` entry point
*   [ ] **Azure:**
    *   [ ] Setup `HcgBlogGenerator.Azure` project
    *   [ ] Implement `BlobStorageFileSystem`
    *   [ ] Implement `AppInsightsLoggerProvider`
    *   [ ] Create `AzureFunctionHandler` entry point

### Console App Enhancements
*   [ ] **Serve Command:**
    *   [ ] Implement `ServeSiteHandler`
    *   [ ] Implement `LocalWebServer` (using Kestrel)
    *   [ ] Add `serve` command to ConsoleApp
    *   [ ] Implement file watching and automatic rebuild (optional)

### Testing
*   [ ] Setup `HcgBlogGenerator.Core.Tests` project
*   [ ] Write unit tests for Core services and handlers
*   [ ] Setup `HcgBlogGenerator.AWS.Tests` project (if needed)
*   [ ] Setup `HcgBlogGenerator.Azure.Tests` project (if needed)
*   [ ] Use `System.IO.Abstractions.TestingHelpers` for file system tests

### Documentation & CI/CD
*   [ ] Setup `build-test.yml` GitHub Actions workflow
*   [ ] Write `docs/usage.md`
*   [ ] Write `docs/plugins.md`
*   [ ] Update `docs/architecture.md`
*   [ ] Create `CONTRIBUTING.md`

## Future Ideas / Nice-to-Haves

*   Support for other template engines (Liquid, Razor)
*   More advanced image processing (resizing, format conversion)
*   Real-time preview updates in `serve` command
*   GUI tool for site management
*   Integration with CMS platforms
*   Support for data files (JSON, YAML) accessible in templates 