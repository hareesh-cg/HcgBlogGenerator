# HcgBlogGenerator - Planning & Architecture

This document outlines the planning, architectural decisions, and technical considerations for the HcgBlogGenerator project.

## 1. High-Level Vision

The primary goal is to create a modern, high-performance, and extensible static site generator using C# 12 and .NET 8. Key characteristics include:

*   **Functionality:** Similar core features to established generators like Jekyll (Markdown processing, frontmatter, layouts, includes, SCSS compilation).
*   **Performance:** Optimized for fast build times, especially for large sites with many posts.
*   **Extensibility:** A robust plugin system allowing users to add custom functionality.
*   **Cloud Native:** Designed to operate seamlessly within serverless environments (AWS Lambda, Azure Functions) using cloud storage (S3, Blob Storage) as the primary file system, while still fully supporting local development.
*   **Maintainability:** Adherence to Clean Architecture principles, strong typing, comprehensive unit testing, and clear separation of concerns.
*   **Developer Experience:** Simple CLI, clear configuration, and good documentation.

## 2. Architectural Design

The project follows the principles of **Clean Architecture** to ensure separation of concerns, testability, and maintainability.

*   **Core (`HcgBlogGenerator.Core`):** Contains the domain models, application logic (use cases/handlers), interfaces for infrastructure concerns (file system, templating, parsing, etc.), and core services. This project has **no dependencies** on specific infrastructure implementations (like local file system, cloud storage, specific databases, or UI frameworks).
    *   **Interfaces:** Define contracts for external dependencies (e.g., `IFileSystem`, `ITemplateEngine`, `IMarkdownParser`).
    *   **Models:** Represent the core data structures (e.g., `SiteConfiguration`, `ContentItem`, `Post`, `Page`).
    *   **Handlers:** Encapsulate the application logic for specific commands (e.g., `BuildSiteHandler`, `InitializeSiteHandler`).
    *   **Services:** Implement core business logic and orchestrate operations (e.g., `SiteGeneratorService`, `ContentDiscoveryService`). Infrastructure *implementations* of the core interfaces are also placed here if they are fundamental and platform-agnostic (e.g., `MarkdownParser` using Markdig).
    *   **Plugins:** Defines the plugin interfaces (`Abstractions`) and includes built-in plugin implementations.
*   **Console App (`HcgBlogGenerator.ConsoleApp`):** Acts as the primary entry point for local development and CLI usage. It depends on `HcgBlogGenerator.Core`.
    *   Handles command-line argument parsing (`System.CommandLine`).
    *   Sets up Dependency Injection, registering core services and specific infrastructure implementations (like `System.IO.Abstractions` for the local file system, Kestrel for the local server).
    *   Invokes the appropriate handlers in the Core project.
    *   Provides the implementation for `ILocalWebServer`.
*   **Cloud Infrastructure (`HcgBlogGenerator.AWS`, `HcgBlogGenerator.Azure`):** Provide implementations for infrastructure interfaces specific to cloud environments. They depend on `HcgBlogGenerator.Core`.
    *   Implement `IFileSystem` using cloud storage APIs (AWS S3 SDK, Azure Blob Storage SDK).
    *   Provide cloud-specific logging providers (`CloudWatchLoggerProvider`, `AppInsightsLoggerProvider`).
    *   Contain the entry points for serverless functions (Lambda Handlers, Azure Function Triggers).
    *   Handle cloud-specific configuration and DI setup.
*   **Testing (`*.Tests`):** Unit tests for each layer, utilizing mocking frameworks (Moq) and testing helpers (`System.IO.Abstractions.TestingHelpers`) to isolate components.

**Key Principles:**

*   **Dependency Rule:** Dependencies flow inwards. Infrastructure and UI depend on Core, but Core does not depend on them.
*   **Dependency Injection:** Used throughout to decouple components and facilitate testing. `Microsoft.Extensions.DependencyInjection` is the chosen container.
*   **Interfaces:** Abstractions are defined in Core, implementations are provided in the outer layers (ConsoleApp, AWS, Azure, or Core itself for library-based implementations).
*   **Async/Await:** All I/O operations are asynchronous.

## 3. Technical Constraints & Decisions

*   **Language/Platform:** C# 12 / .NET 8.
*   **Nullability:** Nullable reference types enabled and enforced.
*   **Core Independence:** `HcgBlogGenerator.Core` must remain independent of specific file systems (local, S3, Blob) and hosting environments.
*   **Performance:** Prioritize efficient file I/O, parsing, and rendering. Avoid unnecessary allocations.
*   **Testability:** All core logic must be unit-testable without infrastructure dependencies.
*   **Configuration:** Simple JSON-based configuration (`_config.json`).
*   **File Handling:** Use `System.IO.Abstractions` to allow swapping file system implementations.

## 4. Chosen Tools & Technology Stack

*   **Language:** C# 12
*   **Framework:** .NET 8
*   **Markdown Processing:** Markdig (High performance, extensible)
*   **Frontmatter Processing:** YamlDotNet (Standard YAML library for .NET)
*   **SCSS Compilation:** SharpScss (LibSass wrapper for .NET)
*   **Template Engine:** Scriban (Fast, powerful, and safe .NET templating engine)
*   **Configuration:** Newtonsoft.Json (Mature and widely used JSON library)
*   **CLI Framework:** System.CommandLine (Modern, official CLI framework)
*   **Logging:** Microsoft.Extensions.Logging (Standard .NET logging abstractions)
*   **Testing:** xUnit (Popular .NET testing framework), Moq (Mocking library)
*   **Dependency Injection:** Microsoft.Extensions.DependencyInjection (Standard DI container)
*   **File Operations Abstraction:** System.IO.Abstractions

## 5. Development Process

*   **Version Control:** Git, hosted on GitHub (assumption).
*   **CI/CD:** GitHub Actions (`.github/workflows/build-test.yml`) for automated builds and tests.
*   **Code Style:** Enforced via `.editorconfig`.
*   **Task Management:** `TASK.md` for tracking progress and backlog.
*   **Documentation:** Maintained in the `/docs` folder and `README.md`.

## 6. Future Considerations / Potential Expansion

*   Support for other template engines (Liquid, Razor) via abstraction.
*   Integration with specific front-end frameworks or libraries.
*   More sophisticated image processing options.
*   Advanced search indexing capabilities.
*   Real-time preview updates in the `serve` command (e.g., using WebSockets).
*   GUI tool for managing sites. 