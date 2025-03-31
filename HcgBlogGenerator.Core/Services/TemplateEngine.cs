using HcgBlogGenerator.Core.Interfaces;

using Microsoft.Extensions.Logging;

using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace HcgBlogGenerator.Core.Services;

public class TemplateEngine : ITemplateEngine {
    private readonly ILogger<TemplateEngine> _logger;
    private readonly ScriptObject _globalContext; // From Scriban.Runtime

    public TemplateEngine(ILogger<TemplateEngine> logger) {
        _logger = logger;
        _globalContext = new ScriptObject(); // From Scriban.Runtime
        _logger.LogDebug("Scriban Template Engine initialized.");
    }

    public ICompiledTemplate? Compile(string templateContent, string templatePath = "unknown") {
        ArgumentNullException.ThrowIfNull(templateContent);

        try {
            var template = Template.Parse(templateContent, templatePath); // 'Template' class from 'Scriban' namespace

            // 'Messages' property comes from the base 'ParsingContext' class in 'Scriban.Parsing' namespace.
            // 'ParserMessageType' enum is in 'Scriban.Parsing' namespace.
            if (template.Messages != null && template.Messages.Any(m => m.Type == ParserMessageType.Error)) {
                // 'SourcePath' property is on the 'Template' class in 'Scriban' namespace.
                _logger.LogError("Error parsing template '{TemplatePath}':", template.SourceFilePath);
                foreach (var error in template.Messages.Where(m => m.Type == ParserMessageType.Error)) {
                    _logger.LogError("- {ErrorMessage} at {SourcePosition}", error.Message, error.Span);
                }
                return null;
            }

            // 'SourcePath' property is on the 'Template' class in 'Scriban' namespace.
            _logger.LogDebug("Successfully parsed template '{TemplatePath}'.", template.SourceFilePath);
            return new CompiledScribanTemplate(template, _logger, _globalContext);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Unexpected error compiling template '{TemplatePath}'.", templatePath);
            return null;
        }
    }

    public void RegisterGlobal(string name, object value) {
        if (string.IsNullOrWhiteSpace(name)) {
            _logger.LogWarning("Attempted to register a global variable with an empty name.");
            return;
        }
        _globalContext[name] = value; // 'ScriptObject' is in 'Scriban.Runtime'
        _logger.LogDebug("Registered global variable '{GlobalName}'.", name);
    }

    public void RegisterHelpers(object helperObject) {
        _globalContext.Import(helperObject); // 'ScriptObject' is in 'Scriban.Runtime'
        _logger.LogDebug("Registered helpers from object of type '{HelperType}'.", helperObject.GetType().Name);
    }

    // --- Private Nested Class for ICompiledTemplate ---

    private class CompiledScribanTemplate : ICompiledTemplate {
        private readonly Template _scribanTemplate; // 'Template' class from 'Scriban' namespace
        private readonly ILogger _logger;
        private readonly ScriptObject _globalContext; // 'ScriptObject' is in 'Scriban.Runtime' namespace

        public CompiledScribanTemplate(Template scribanTemplate, ILogger logger, ScriptObject globalContext) {
            _scribanTemplate = scribanTemplate;
            _logger = logger;
            _globalContext = globalContext;
        }

        public async Task<string> RenderAsync(object model, CancellationToken cancellationToken = default) {
            try {
                var context = new TemplateContext // 'TemplateContext' is in 'Scriban.Runtime' namespace
                {
                    EnableRelaxedMemberAccess = true,
                    StrictVariables = false
                };

                context.PushGlobal(_globalContext);

                var scriptObject = new ScriptObject(); // 'ScriptObject' is in 'Scriban.Runtime' namespace
                scriptObject.Import(model);
                context.PushGlobal(scriptObject);

                context.CancellationToken = cancellationToken;

                var output = await _scribanTemplate.RenderAsync(context);

                return output;
            }
            catch (ScriptRuntimeException runtimeEx) // 'ScriptRuntimeException' is in 'Scriban.Runtime' namespace
            {
                // 'SourcePath' property is on the 'Template' class in 'Scriban' namespace.
                _logger.LogError(runtimeEx, "Runtime error rendering template '{TemplatePath}' at {SourcePosition}: {ErrorMessage}",
                    _scribanTemplate.SourceFilePath, runtimeEx.Span, runtimeEx.Message);
                return string.Empty;
            }
            catch (OperationCanceledException) {
                // 'SourcePath' property is on the 'Template' class in 'Scriban' namespace.
                _logger.LogWarning("Template rendering cancelled for '{TemplatePath}'.", _scribanTemplate.SourceFilePath);
                return string.Empty;
            }
            catch (Exception ex) {
                // 'SourcePath' property is on the 'Template' class in 'Scriban' namespace.
                _logger.LogError(ex, "Unexpected error rendering template '{TemplatePath}'.", _scribanTemplate.SourceFilePath);
                return string.Empty;
            }
        }
    }
}
// --- END OF FILE TemplateEngine.cs ---
