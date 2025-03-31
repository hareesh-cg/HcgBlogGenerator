 using HcgBlogGenerator.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime; // Required for ScriptObject

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// Implements the ITemplateEngine interface using the Scriban library.
/// Handles parsing, compiling, and rendering templates.
/// </summary>
public class TemplateEngine : ITemplateEngine
{
    private readonly ILogger<TemplateEngine> _logger;
    private readonly ScriptObject _globalContext; // Stores globally registered objects/functions

    public TemplateEngine(ILogger<TemplateEngine> logger)
    {
        _logger = logger;
        _globalContext = new ScriptObject();
        _logger.LogDebug("Scriban Template Engine initialized.");
    }

    /// <inheritdoc />
    public ICompiledTemplate? Compile(string templateContent, string templatePath = "unknown")
    {
        ArgumentNullException.ThrowIfNull(templateContent);

        try
        {
            // Parse the template. Scriban performs lexing and parsing here.
            // The Template object itself represents the compiled form.
            var template = Template.Parse(templateContent, templatePath);

            // Check for parsing errors
            if (template.HasErrors)
            {
                _logger.LogError("Error parsing template '{TemplatePath}':", templatePath);
                foreach (var error in template.Messages)
                {
                    _logger.LogError("- {ErrorMessage}", error.Message);
                }
                return null; // Indicate failure
            }

            _logger.LogDebug("Successfully parsed template '{TemplatePath}'.", templatePath);
            return new CompiledScribanTemplate(template, _logger, _globalContext);
        }
        catch (SyntaxErrorException syntaxEx)
        {
            _logger.LogError(syntaxEx, "Syntax error parsing template '{TemplatePath}': {ErrorMessage}", templatePath, syntaxEx.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error compiling template '{TemplatePath}'.", templatePath);
            return null;
        }
    }

    /// <inheritdoc />
    public void RegisterGlobal(string name, object value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Attempted to register a global variable with an empty name.");
            return;
        }
        _globalContext[name] = value;
        _logger.LogDebug("Registered global variable '{GlobalName}'.", name);
    }

    /// <inheritdoc />
    public void RegisterHelpers(object helperObject)
    {
        // Scriban uses ScriptObject to import functions/properties from an object.
        // We add the helper object directly to the global context.
        // For more complex scenarios (like naming conflicts), might need custom import logic.
        _globalContext.Import(helperObject);
        _logger.LogDebug("Registered helpers from object of type '{HelperType}'.", helperObject.GetType().Name);
    }

    // --- Private Nested Class for ICompiledTemplate ---

    /// <summary>
    /// Represents a Scriban template that has been parsed and is ready for rendering.
    /// </summary>
    private class CompiledScribanTemplate : ICompiledTemplate
    {
        private readonly Template _scribanTemplate;
        private readonly ILogger _logger;
        private readonly ScriptObject _globalContext;

        public CompiledScribanTemplate(Template scribanTemplate, ILogger logger, ScriptObject globalContext)
        {
            _scribanTemplate = scribanTemplate;
            _logger = logger;
            _globalContext = globalContext; // Reference to shared global context
        }

        /// <inheritdoc />
        public async Task<string> RenderAsync(object model, CancellationToken cancellationToken = default)
        {
            try
            {
                // Create the main context for rendering
                var context = new TemplateContext
                {
                    // Enable C# properties/methods to be called directly (adjust security as needed)
                    MemberRenamer = MemberRenamer.CamelCase, // Optional: Use camelCase for C# members in templates
                    MemberFilter = MemberFilter.Default,     // Optional: Control which C# members are accessible
                    EnableRelaxedMemberAccess = true,        // Optional: Allows accessing dictionary keys like properties
                    StrictVariables = false                  // Optional: Set to true to throw errors for undefined variables
                };

                // Push the global context first
                context.PushGlobal(_globalContext);

                // Push the specific model for this rendering call
                var scriptObject = new ScriptObject();
                scriptObject.Import(model, renamer: MemberRenamer.CamelCase); // Import model properties/methods
                context.PushGlobal(scriptObject); // Push model data onto the context stack

                // Add cancellation support
                context.CancellationToken = cancellationToken;

                // Render the template asynchronously
                var output = await _scribanTemplate.RenderAsync(context);

                // Check for rendering errors (though many errors are caught at parse time)
                if (context.HasErrors)
                {
                    _logger.LogError("Error rendering template '{TemplatePath}':", _scribanTemplate.SourcePath);
                    foreach (var error in context.Messages)
                    {
                        _logger.LogError("- {ErrorMessage}", error.Message);
                    }
                    // Return empty or throw? Returning empty for now.
                    return string.Empty;
                }

                return output;
            }
            catch (ScriptRuntimeException runtimeEx)
            {
                _logger.LogError(runtimeEx, "Runtime error rendering template '{TemplatePath}': {ErrorMessage}", _scribanTemplate.SourcePath, runtimeEx.Message);
                return string.Empty; // Or re-throw, or return error message
            }
            catch (OperationCanceledException)
            {
                 _logger.LogWarning("Template rendering cancelled for '{TemplatePath}'.", _scribanTemplate.SourcePath);
                 return string.Empty; // Or re-throw
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error rendering template '{TemplatePath}'.", _scribanTemplate.SourcePath);
                return string.Empty; // Or re-throw
            }
        }
    }
}