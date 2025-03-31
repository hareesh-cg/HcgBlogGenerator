# HcgBlogGenerator

[![Build Status](https://github.com/hareesh-cg/HcgBlogGenerator/actions/workflows/build-test.yml/badge.svg)](https://github.com/hareesh-cg/HcgBlogGenerator/actions/workflows/build-test.yml) <!-- TODO: Update with actual repo path -->
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

HcgBlogGenerator is a high-performance, extensible static site generator built with C# 12, designed for creating blogs and websites. Inspired by tools like Jekyll, it processes Markdown files with YAML frontmatter, compiles SCSS, utilizes a powerful templating engine (Scriban), and generates optimized static HTML sites.

A key design goal is its ability to run efficiently in serverless environments like AWS Lambda and Azure Functions, operating directly on cloud storage (S3, Blob Storage), while also functioning perfectly on a local file system.

## Features

*   **Markdown Processing:** Converts Markdown to HTML using Markdig, including frontmatter extraction (YamlDotNet).
*   **SCSS Compilation:** Compiles SCSS/SASS to CSS using SharpScss.
*   **Templating:** Flexible layouts and partials powered by Scriban.
*   **Plugin Architecture:** Easily extend functionality with custom plugins. Built-in plugins for RSS, Sitemap, SEO tags, and `robots.txt`.
*   **Performance:** Optimized for speed and efficiency, suitable for large sites.
*   **Cross-Platform:** Runs anywhere .NET runs (Windows, macOS, Linux).
*   **Cloud Native:** Designed for seamless integration with AWS (S3) and Azure (Blob Storage).
*   **Development Server:** Includes a local server for previewing changes (`serve` command).
*   **Clean Architecture:** Core logic is decoupled from specific infrastructure (file system, cloud providers).
*   **Modern C#:** Leverages C# 12 features and best practices.

*(Planned Features: Image Optimization, Pagination, Categories/Tags, Search, Reading Time, Related Posts, Asset Fingerprinting, etc.)*

## Tech Stack

*   **Core Language:** C# 12 / .NET 8
*   **Markdown:** Markdig
*   **Frontmatter:** YamlDotNet
*   **SCSS:** SharpScss
*   **Templating:** Scriban
*   **Configuration:** Newtonsoft.Json
*   **File System Abstraction:** System.IO.Abstractions
*   **Dependency Injection:** Microsoft.Extensions.DependencyInjection
*   **Logging:** Microsoft.Extensions.Logging
*   **CLI:** System.CommandLine
*   **Testing:** xUnit, Moq

## Getting Started

*(Instructions on how to install, initialize a new site, build the site, and use the development server will go here.)*

### Prerequisites

*   .NET 8 SDK

### Installation

*(Details TBD - e.g., via NuGet package, dotnet tool, or cloning the repo)*

### Basic Usage

1.  **Initialize a new site:**
    ```bash
    dotnet run --project HcgBlogGenerator.ConsoleApp -- init <directory>
    # or if installed as a tool:
    # hcgblog init <directory>
    ```
2.  **Build the site:**
    ```bash
    dotnet run --project HcgBlogGenerator.ConsoleApp -- build <source_directory> -o <output_directory>
    # or if installed as a tool:
    # hcgblog build <source_directory> -o <output_directory>
    ```
3.  **Serve the site locally:**
    ```bash
    dotnet run --project HcgBlogGenerator.ConsoleApp -- serve <source_directory>
    # or if installed as a tool:
    # hcgblog serve <source_directory>
    ```

*(More detailed usage examples and configuration options will be added.)*

## Documentation

*   **Architecture:** [docs/architecture.md](docs/architecture.md) (Overview of the internal design)
*   **Usage Guide:** [docs/usage.md](docs/usage.md) (Detailed user instructions)
*   **Plugin Development:** [docs/plugins.md](docs/plugins.md) (How to create custom plugins)

## Contributing

Contributions are welcome! Please refer to the `CONTRIBUTING.md` file (to be created) for guidelines. Key documents for contributors:

*   **Planning & Architecture:** [PLANNING.md](PLANNING.md)
*   **Task Tracking:** [TASK.md](TASK.md)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
