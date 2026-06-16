using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Security;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Application.Services;
using Scriban;
using Scriban.Runtime;

namespace Modules.Lhdn.Infrastructure.Services;

public class ScribanTemplateRendererService : ITemplateRendererService
{
    private readonly ILogger<ScribanTemplateRendererService> _logger;
    private readonly ConcurrentDictionary<string, Template> _templateCache = new();
    private readonly Assembly _assembly;

    public ScribanTemplateRendererService(ILogger<ScribanTemplateRendererService> logger)
    {
        _logger = logger;
        _assembly = typeof(ScribanTemplateRendererService).Assembly;
    }

    public string Render(string templateName, object model)
    {
        var template = _templateCache.GetOrAdd(templateName, LoadAndCompileTemplate);

        var scriptObject = new ScriptObject();
        scriptObject.Import(model);

        // Enforces strict 2-decimal formatting for LHDN Schematron currency compliance
        scriptObject.Import("format_amount", new Func<decimal, string>(amount => amount.ToString("0.00")));
        
        // Prevents XML injection attacks by escaping raw strings before insertion
        scriptObject.Import("xml_escape", new Func<string, string>(text => 
            string.IsNullOrEmpty(text) ? string.Empty : SecurityElement.Escape(text)));

        var context = new TemplateContext { StrictVariables = true };
        context.PushGlobal(scriptObject);

        return template.Render(context);
    }

    private Template LoadAndCompileTemplate(string templateName)
    {
        var resourceName = $"Modules.Lhdn.Infrastructure.Templates.{templateName}";
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        
        if (stream == null)
        {
            _logger.LogError("Template resource {ResourceName} not found in assembly {AssemblyName}.", resourceName, _assembly.FullName);
            throw new FileNotFoundException($"Embedded template '{resourceName}' not found.");
        }

        using var reader = new StreamReader(stream);
        var templateContent = reader.ReadToEnd();

        var template = Template.Parse(templateContent, templateName);

        if (template.HasErrors)
        {
            var errors = string.Join("\n", template.Messages);
            _logger.LogError("Failed to parse Scriban template {TemplateName}. Errors: {Errors}", templateName, errors);
            throw new InvalidOperationException($"Template compilation failed for {templateName}: {errors}");
        }

        return template;
    }
}
