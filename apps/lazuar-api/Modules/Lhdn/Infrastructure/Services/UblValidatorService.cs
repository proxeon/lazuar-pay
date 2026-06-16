using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using BuildingBlocks.Domain;
using Modules.Lhdn.Application.Services;

namespace Modules.Lhdn.Infrastructure.Services;

/// <summary>
/// A high-performance Singleton that compiles the heavy OASIS UBL 2.1 schemas once at startup.
/// Validates outbound XML payloads in memory to catch structural errors before hitting LHDN APIs.
/// </summary>
public class UblValidatorService : IUblValidatorService
{
    private readonly XmlSchemaSet _schemaSet;

    public UblValidatorService()
    {
        _schemaSet = new XmlSchemaSet
        {
            XmlResolver = new EmbeddedResourceXmlResolver()
        };

        var assembly = typeof(UblValidatorService).Assembly;

        // W3C Schema files contain <!DOCTYPE> declarations. 
        // We must explicitly allow DTD parsing to load them.
        // This is safe because we are only reading embedded assembly resources, not user input.
        var xsdSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse 
        };

        // Pre-load ALL embedded XSDs to bypass internal schemaLocation filename mismatches.
        // The XmlSchemaSet will automatically map them by their internal targetNamespace.
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (resourceName.Contains(".Schemas.") && resourceName.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    _schemaSet.Add(null, XmlReader.Create(stream, xsdSettings));
                }
            }
        }

        if (_schemaSet.Count > 0)
        {
            _schemaSet.Compile();
        }
    }

    public void Validate(string xmlString, string documentType)
    {
        if (_schemaSet.Count == 0) return;

        var errors = new List<string>();
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = _schemaSet
        };

        settings.ValidationEventHandler += (sender, args) =>
        {
            if (args.Severity == XmlSeverityType.Error || args.Severity == XmlSeverityType.Warning)
            {
                errors.Add($"[Line {args.Exception.LineNumber}, Pos {args.Exception.LinePosition}]: {args.Message}");
            }
        };

        using var stringReader = new StringReader(xmlString);
        using var xmlReader = XmlReader.Create(stringReader, settings);

        while (xmlReader.Read()) { }

        if (errors.Count > 0)
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule(
                $"XML Schema Validation Failed:\n{string.Join("\n", errors)}"));
        }
    }

    private class EmbeddedResourceXmlResolver : XmlResolver
    {
        public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            var assembly = typeof(UblValidatorService).Assembly;
            var fileName = Path.GetFileName(absoluteUri.LocalPath);

            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (resourceName.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return assembly.GetManifestResourceStream(resourceName);
                }
            }

            return null;
        }
    }
}
