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
            // Custom resolver to trace <xsd:import> tags across embedded resources
            XmlResolver = new EmbeddedResourceXmlResolver()
        };

        var assembly = typeof(UblValidatorService).Assembly;

        // Load root Invoice Schema
        using var invoiceStream = assembly.GetManifestResourceStream("Modules.Lhdn.Infrastructure.Schemas.UBL-Invoice-2.1.xsd");
        if (invoiceStream != null)
        {
            _schemaSet.Add("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", XmlReader.Create(invoiceStream));
        }

        // Load root CreditNote Schema
        using var creditNoteStream = assembly.GetManifestResourceStream("Modules.Lhdn.Infrastructure.Schemas.UBL-CreditNote-2.1.xsd");
        if (creditNoteStream != null)
        {
            _schemaSet.Add("urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2", XmlReader.Create(creditNoteStream));
        }

        if (invoiceStream != null || creditNoteStream != null)
        {
            _schemaSet.Compile();
        }
    }

    public void Validate(string xmlString, string documentType)
    {
        // Bypass validation if developer hasn't downloaded the XSD files into the directory yet
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

        // Reads the entire document to trigger all validation events
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
            
            // XSD internal references use relative paths (e.g., ../common/UBL-CommonBasicComponents-2.1.xsd)
            // We format the URI path to match the C# Embedded Resource dot-notation standard.
            var path = absoluteUri.LocalPath.Replace("/", ".").Replace("\\", ".");

            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (resourceName.EndsWith(path, StringComparison.OrdinalIgnoreCase))
                {
                    return assembly.GetManifestResourceStream(resourceName);
                }
            }

            return null;
        }
    }
}
