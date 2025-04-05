# HcgBlogGenerator

[![Build Status](https://img.shields.io/github/actions/workflow/status/hareesh-cg/HcgBlogGenerator/dotnet.yml?branch=main)](https://github.com/hareesh-cg/HcgBlogGenerator/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**HcgBlogGenerator** is a fast, flexible, and modern static site generator (SSG) for blogs and websites, built entirely with C# and .NET 8. Inspired by tools like Jekyll and Hugo, it transforms Markdown files, SCSS, and configuration into clean, performant static HTML websites.

Designed with cloud-native principles in mind, it can be easily run locally via CLI or deployed to serverless platforms like AWS Lambda or Azure Functions to generate sites directly from cloud storage.

**Core Principles:**

*   **Clean Code:** Emphasizes maintainability and separation of concerns.
*   **Performance:** Optimized for speed and memory efficiency, suitable for large sites.
*   **.NET Native:** Leverages modern C# features and the performance of the .NET platform.
*   **Extensibility:** Features a plugin system for adding custom functionality.
*   **Cloud Ready:** Includes adapters for AWS S3 and Azure Blob Storage (in separate projects).

## Features

*   **Core:**
    *   `build`: Generates the static site.
    *   `serve`: Starts a local development server.
    *   (`init`): Creates a new site scaffold (Future).
    *   (`watch`): Rebuilds site automatically on changes (Future).
*   **Content Management:**
    *   **Markdown Processing:** Uses [Markdig](https://github.com/xoofx/markdig) for CommonMark compliant parsing with extensions.
    *   **Front Matter:** Extracts metadata (YAML format) from Markdown files (title, date, layout, tags, categories, custom data).
    *   **Draft Support:** Manage unpublished content via `draft: true` front matter.
*   **Blogging Features:**
    *   **Automatic Post/Page Handling:** Differentiates content based on directory structure or front matter.
    *   **Taxonomies:** Automatic generation of Category and Tag listing pages.
    *   **Reading Time:** Calculates estimated reading time for posts (via Plugin).
    *   **Related Posts:** Functionality planned.
    *   **Pagination:** Blog index and taxonomy pages support pagination (Future).
*   **Templating:**
    *   **Scriban Engine:** Uses the fast and powerful [Scriban](https://github.com/scriban/scriban) templating language (Liquid compatible).
    *   **Layouts:** Define reusable page structures (`_layouts/default.html`).
    *   **Includes/Partials:** Create reusable template snippets (`_includes/header.html`).
*   **Styling:**
    *   **SCSS/SASS Compilation:** Uses [LibSassHost](https://github.com/Taritsyn/LibSassHost) to compile SCSS/SASS to CSS.
*   **SEO & Output:**
    *   **SEO Plugin:** Generates optimized `<title>`, meta descriptions, canonical URLs, Open Graph tags, and Twitter Card tags.
    *   **Sitemap Generation:** Automatically generates `sitemap.xml` (via Plugin).
    *   **Robots.txt Generation:** Generates `robots.txt` (via Plugin).
    *   **RSS Feed Generation:** Automatically generates `feed.xml` (via Plugin).
    *   **Configurable Permalinks:** Define URL structures for posts and pages.
    *   **Static File Copying:** Copies assets from a `static` directory directly to the output.
*   **Extensibility:**
    *   **Plugin System:** Hook into various build pipeline stages (`PreBuild`, `PostContentProcessing`, `PostBuild`, `BuildComplete`) to modify content or generate artifacts.
*   **Cloud Integration (Separate Projects):**
    *   AWS S3 & Lambda Adapters (`HcgBlogGenerator.Aws`).
    *   Azure Blob Storage & Functions Adapters (`HcgBlogGenerator.Azure`).

## Installation

**Prerequisites:**

*   .NET 8 SDK or later.

**Install as .NET Tool:**

Once published to NuGet (or built locally):

```bash
# Install globally
dotnet tool install --global HcgBlogGenerator.Tool --version <VERSION>

# --- OR ---

# Install locally (requires a tool manifest file: dotnet new tool-manifest)
dotnet tool install HcgBlogGenerator.Tool --version <VERSION>

# Verify installation
hcg-blog --version


## Getting Started

1.  **Create a New Site:**
    
    -   **(Future):** Use the init command: hcg-blog init MyNewBlog
        
    -   **Currently:** Manually create a directory structure similar to the samples/sample-blog directory provided in this repository, or copy the sample blog.
        
2.  **Directory Structure:**
	```
	MyNewBlog/
	├── config.json           # Main site configuration
	├── content/              # Markdown files for posts, pages, etc.
	│   ├── posts/
	│   ├── pages/
	│   └── drafts/           # (Optional) Markdown files for draft posts
	├── layouts/              # Scriban layout templates (default.html, post.html)
	├── includes/             # Scriban partial templates (header.html, footer.html)
	├── static/               # Static assets (images, JS, fonts) - copied as-is
	└── styles/               # SCSS/SASS source files (main.scss)
	```
3.  **Configure:** Edit config.json to set your site's title, base URL, permalinks, etc. (See Configuration section).
    
4.  **Add Content:** Write blog posts and pages as Markdown files (.md) in the content directory. Use YAML front matter for metadata.
	```
	---
	title: My Awesome Post
	date: 2024-03-15T10:00:00Z
	layout: post
	tags: [Example, CSharp]
	categories: [Tutorials]
	# Add any custom data here
	customField: some value
	---

	## Post Content

	Write your content using **Markdown**.
	```
5.  **Add Templates:** Create/modify Scriban templates in `layouts/` and `includes/`.
    
6.  **Add Styles:** Write SCSS/SASS in the `styles/` directory. The `main.scss` (or as configured) will be compiled.
    
7.  **Build:** Run the build command from your site's root directory:
	```
	hcg-blog build
	```
	Or specify source/output:
	```
	hcg-blog build -s . -o ./public_html
	```
	This generates the static site into the output directory (default: `_site`).
	
8. **Preview:** Use the serve command to preview locally:
	```
	hcg-blog serve
	```
	Or specify output directory and port:
	```
	hcg-blog build -s . -o ./public_html
	```
	Open your browser to `http://localhost:8080` (or the specified port).
	
9.  **Deploy:** Copy the contents of the output directory (_site or specified -o path) to your web host or static hosting provider (e.g., S3, Azure Static Web Apps, Netlify, Vercel, GitHub Pages).


## Usage (CLI Commands)

-   **hcg-blog build [options]**: Builds the static site.
    
    -   -s, --source <DIR>: Path to the source directory (default: current directory).
        
    -   -o, --output <DIR>: Path to the output directory (default: _site relative to source).
        
    -   -c, --config <FILE>: Path to the configuration file (default: config.json relative to source).
        
    -   (Future)  --drafts: Build draft posts.
        
    -   (Future)  --future: Build posts with future dates.
        
-   **hcg-blog serve [options]**: Builds (implicitly, future: optionally watches) and serves the site locally.
    
    -   -s, --source <DIR>: Path to the source directory (default: current directory). Used to find default output/config.
        
    -   -o, --output <DIR>: Path to the directory to serve (default: _site relative to source). Build command output is served.
        
    -   -p, --port <PORT>: Port number to use (default: 8080).
        
    -   (Future)  --watch: Automatically rebuild site on file changes.
        
    -   (Future)  --drafts: Build and serve draft posts.
        
    -   (Future)  --future: Build and serve posts with future dates.
        
-   **hcg-blog init <DIR> (Future)**: Creates a new site structure in the specified directory.
    
-   **hcg-blog --version**: Displays the tool version.
    
-   **hcg-blog --help**: Displays help information.
    

## Configuration (config.json)

Key configuration options (see samples/sample-blog/config.json for a full example):

-   baseUrl: Absolute base URL of the deployed site (e.g., "[https://www.example.com](https://www.google.com/url?sa=E&q=https%3A%2F%2Fwww.example.com)"). Crucial for feeds, sitemaps, canonical URLs.
    
-   title: The main title of the site.
    
-   description: A short description for the site (used in meta tags, feeds).
    
-   language: Site language code (e.g., "en-US").
    
-   postsPerPage: Number of posts on paginated listing pages (used by pagination feature - Future).
    
-   contentDirectory, templateDirectory, includesDirectory, staticDirectory, stylesDirectory: Paths to key source directories (relative to source root).
    
-   styleEntryPoint: Main SCSS/SASS file to compile (relative to stylesDirectory).
    
-   outputDirectory: Directory name for generated site output (relative to execution or specified output path).
    
-   postPermalink, pagePermalink: URL structure template (uses placeholders like :year, :month, :day, :slug).
    
-   buildDrafts, buildFutureDated: Booleans to control draft/future post visibility.
    
-   tagUrlBasePath, categoryUrlBasePath: Base URL paths for generated tag/category pages.
    
-   rss: Object with RSS feed settings (enabled, outputPath, maxItems).
    
-   extraData: A general-purpose object for custom site-wide data accessible in templates via config.ExtraData.
    

## Templating (Scriban)

-   **Engine:** Uses Scriban. See [Scriban Language Documentation](https://www.google.com/url?sa=E&q=https%3A%2F%2Fgithub.com%2Fscriban%2Fscriban%2Fblob%2Fmaster%2Fdoc%2Flanguage.md).
    
-   **Layouts:** Place in layouts/. Specify in front matter (layout: post.html) or rely on defaults. Use {{ layout = 'base.html' }} within a template to wrap it in another layout. The content of the inner template is typically available via {{ content }} in the outer layout.
    
-   **Includes:** Place in includes/. Use {{ include 'partial_name.html' }}. Includes are resolved relative to the includesDirectory.
    
-   **Data Access:**
    
    -   **Current Item Properties:** Directly access properties of the current page/post model (e.g., {{ Title }}, {{ HtmlContent }}, {{ Date }}, {{ ReadingTimeMinutes }}, {{ FrontMatter }}, {{ Seo }}).
        
    -   **Configuration:** Access via {{ config }} (e.g., {{ config.BaseUrl }}, {{ config.Title }}, {{ config.ExtraData.MyCustomValue }}).
        
    -   **Site Data:** Access site-wide lists and data via {{ site }} (e.g., {{ site.Posts }}, {{ site.Pages }}, {{ site.Taxonomies }}).
        
    -   **Built-in Objects:** Use Scriban's built-ins (e.g., date.now, string.downcase, array.size).
        
    -   **Custom Functions:**  {{ my_value | slugify }} is available.
        

## Plugins

Plugins extend the build process. They are registered via DI and executed by the PluginManager.

-   **ReadingTimePlugin:** Calculates ReadingTimeMinutes for posts. Runs at PostContentProcessing.
    
-   **SeoPlugin:** Generates Seo data object (Title, Description, OG, Twitter) for content items. Runs at PostContentProcessing.
    
-   **RobotsPlugin:** Generates robots.txt. Runs at PostBuild.
    
-   **SitemapPlugin:** Generates sitemap.xml. Runs at PostBuild.
    
-   **RssPlugin:** Generates feed.xml. Runs at PostBuild.
    

## Contributing

Contributions are welcome! Please feel free to open an issue on GitHub to report bugs, suggest features, or ask questions.

(Detailed contribution guidelines, including pull request process and coding standards, can be added later.)

## License

This project is licensed under the **MIT License**. See the [LICENSE](https://www.google.com/url?sa=E&q=LICENSE) file for details.