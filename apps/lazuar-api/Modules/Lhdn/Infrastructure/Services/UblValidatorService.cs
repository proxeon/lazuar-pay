using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        // We explicitly allow DTD parsing to load them securely from our embedded resources.
        var xsdSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse };

        // 1. Manually preload the W3C Signature schema to bypass DTD restrictions during dynamic compilation
        var w3cResourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("UBL-xmldsig-core-schema-2.1.xsd", StringComparison.OrdinalIgnoreCase));
        
        if (w3cResourceName != null)
        {
            using var w3cStream = assembly.GetManifestResourceStream(w3cResourceName);
            if (w3cStream != null)
            {
                _schemaSet.Add("http://www.w3.org/2000/09/xmldsig#", XmlReader.Create(w3cStream, xsdSettings));
            }
        }

        // 2. Load Root Invoice Schema
        var invoiceResourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("UBL-Invoice-2.1.xsd", StringComparison.OrdinalIgnoreCase));
        
        if (invoiceResourceName != null)
        {
            using var stream = assembly.GetManifestResourceStream(invoiceResourceName);
            if (stream != null) _schemaSet.Add("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", XmlReader.Create(stream, xsdSettings));
        }

        // 3. Load Root CreditNote Schema
        var cnResourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("UBL-CreditNote-2.1.xsd", StringComparison.OrdinalIgnoreCase));
        
        if (cnResourceName != null)
        {
            using var stream = assembly.GetManifestResourceStream(cnResourceName);
            if (stream != null) _schemaSet.Add("urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2", XmlReader.Create(stream, xsdSettings));
        }

        if (_schemaSet.Count > 0)
        {
            // The compiler will now use EmbeddedResourceXmlResolver to find all other safe dependencies
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

        // Read through to trigger events
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
