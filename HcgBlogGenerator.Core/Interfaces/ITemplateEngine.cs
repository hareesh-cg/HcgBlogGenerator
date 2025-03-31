 namespace HcgBlogGenerator.Core.Interfaces;

/// <summary>
/// Represents a compiled template that can be rendered with specific data.
/// </summary>
public interface ICompiledTemplate
{
    /// <summary>
    /// Renders the compiled template using the provided data model.
    /// </summary>
    /// <param name="model">The data object (e.g., TemplateData) containing variables accessible within the template.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous rendering operation. The task result contains the rendered output string.</returns>
    Task<string> RenderAsync(object model, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the contract for a template engine responsible for parsing and compiling templates.
/// </summary>
public interface ITemplateEngine
{
    /// <summary>
    /// Parses and compiles a template string.
    /// </summary>
    /// <param name="templateContent">The raw string content of the template.</param>
    /// <param name="templatePath">The path or identifier of the template, used for error reporting and potentially for resolving relative includes.</param>
    /// <returns>An <see cref="ICompiledTemplate"/> object representing the parsed template, ready for rendering. Returns null if parsing fails.</returns>
    /// <exception cref="ArgumentNullException">Thrown if templateContent is null.</exception>
    /// <remarks>
    /// Implementations should handle caching of compiled templates where appropriate for performance.
    /// </remarks>
    ICompiledTemplate? Compile(string templateContent, string templatePath = "unknown");

    /// <summary>
    /// Registers a custom object or function to be accessible within templates.
    /// </summary>
    /// <param name="name">The name under which the object/function will be accessible in the template.</param>
    /// <param name="value">The object or delegate to register.</param>
    void RegisterGlobal(string name, object value);

    /// <summary>
    /// Registers template helpers (e.g., custom functions, filters) from a specific object.
    /// The mechanism for discovering helpers depends on the underlying engine (e.g., reflection).
    /// </summary>
    /// <param name="helperObject">An object containing methods or properties to be registered as template helpers.</param>
    void RegisterHelpers(object helperObject);
}