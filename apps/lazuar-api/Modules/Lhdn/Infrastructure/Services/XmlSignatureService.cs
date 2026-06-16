using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using Modules.Lhdn.Application.Services;

namespace Modules.Lhdn.Infrastructure.Services;

public class XmlSignatureService : IXmlSignatureService
{
    private const string XadesNamespaceUrl = "http://uri.etsi.org/01903/v1.3.2#";
    private const string SignatureNamespaceUrl = "http://www.w3.org/2000/09/xmldsig#";
    private const string ExtNamespaceUrl = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    private const string SigNamespaceUrl = "urn:oasis:names:specification:ubl:schema:xsd:CommonSignatureComponents-2";
    private const string SacNamespaceUrl = "urn:oasis:names:specification:ubl:schema:xsd:SignatureAggregateComponents-2";
    private const string SbcNamespaceUrl = "urn:oasis:names:specification:ubl:schema:xsd:SignatureBasicComponents-2";

    static XmlSignatureService()
    {
        CryptoConfig.AddAlgorithm(typeof(XmlDsigExcC14NTransform), "http://www.w3.org/2001/10/xml-exc-c14n#");
    }

    public void SignDocument(XmlDocument document, X509Certificate2 certificate)
    {
        var root = document.DocumentElement ?? throw new InvalidOperationException("XML document has no root element.");

        var ublExtensions = document.CreateElement("ext", "UBLExtensions", ExtNamespaceUrl);
        var ublExtension = document.CreateElement("ext", "UBLExtension", ExtNamespaceUrl);
        var extensionUri = document.CreateElement("ext", "ExtensionURI", ExtNamespaceUrl);
        extensionUri.InnerText = "urn:oasis:names:specification:ubl:dsig:enveloped:xades";
        
        var extensionContent = document.CreateElement("ext", "ExtensionContent", ExtNamespaceUrl);
        var ublDocumentSignatures = document.CreateElement("sig", "UBLDocumentSignatures", SigNamespaceUrl);
        ublDocumentSignatures.SetAttribute("xmlns:sac", SacNamespaceUrl);
        ublDocumentSignatures.SetAttribute("xmlns:sbc", SbcNamespaceUrl);

        var signatureInformation = document.CreateElement("sac", "SignatureInformation", SacNamespaceUrl);
        
        var cbcId = document.CreateElement("cbc", "ID", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
        cbcId.InnerText = "urn:oasis:names:specification:ubl:signature:1";
        
        var sbcReferencedSignatureId = document.CreateElement("sbc", "ReferencedSignatureID", SbcNamespaceUrl);
        sbcReferencedSignatureId.InnerText = "urn:oasis:names:specification:ubl:signature:Invoice";

        signatureInformation.AppendChild(cbcId);
        signatureInformation.AppendChild(sbcReferencedSignatureId);
        
        ublDocumentSignatures.AppendChild(signatureInformation);
        extensionContent.AppendChild(ublDocumentSignatures);
        ublExtension.AppendChild(extensionUri);
        ublExtension.AppendChild(extensionContent);
        ublExtensions.AppendChild(ublExtension);

        root.InsertBefore(ublExtensions, root.FirstChild);

        var rsaKey = certificate.GetRSAPrivateKey() ?? throw new InvalidOperationException("Certificate does not contain an RSA private key.");
        var signedXml = new SignedXml(document) { SigningKey = rsaKey };

        // Explicitly set the Signature ID to match LHDN's expected format
        signedXml.Signature.Id = "signature";
        signedXml.SignedInfo!.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
        signedXml.SignedInfo.CanonicalizationMethod = "http://www.w3.org/2001/10/xml-exc-c14n#";

        var docReference = new Reference { Uri = "", Id = "id-doc-signed-data" };

        // Use ds prefix and namespace for XPath transforms to ensure proper serialization
        var extXPathElement = document.CreateElement("ds", "XPath", SignatureNamespaceUrl);
        extXPathElement.InnerText = "not(//ancestor-or-self::ext:UBLExtensions)";
        var xpathTransform1 = new XmlDsigXPathTransform();
        xpathTransform1.LoadInnerXml(extXPathElement.SelectNodes(".")!);
        docReference.AddTransform(xpathTransform1);

        var cacXPathElement = document.CreateElement("ds", "XPath", SignatureNamespaceUrl);
        cacXPathElement.InnerText = "not(//ancestor-or-self::cac:Signature)";
        var xpathTransform2 = new XmlDsigXPathTransform();
        xpathTransform2.LoadInnerXml(cacXPathElement.SelectNodes(".")!);
        docReference.AddTransform(xpathTransform2);

        docReference.AddTransform(new XmlDsigExcC14NTransform());
        docReference.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256";
        signedXml.AddReference(docReference);

        var dataObject = CreateXadesObject(document, certificate);
        signedXml.AddObject(dataObject);

        var propsReference = new Reference
        {
            Uri = "#id-xades-signed-props",
            DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256"
        };
        signedXml.AddReference(propsReference);

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificate));
        signedXml.KeyInfo = keyInfo;

        signedXml.ComputeSignature();
        var signatureElement = signedXml.GetXml();

        // Enforce the 'ds:' prefix across all generated signature nodes for LHDN regex parser compatibility
        ApplyPrefix(signatureElement, "ds", SignatureNamespaceUrl);

        if (signatureElement.Attributes?["xmlns"] != null)
        {
            signatureElement.Attributes.RemoveNamedItem("xmlns");
        }

        signatureInformation.AppendChild(document.ImportNode(signatureElement, true));
    }

    private static void ApplyPrefix(XmlNode node, string prefix, string namespaceUri)
    {
        if (node.NamespaceURI == namespaceUri)
        {
            node.Prefix = prefix;
        }

        foreach (XmlNode child in node.ChildNodes)
        {
            ApplyPrefix(child, prefix, namespaceUri);
        }
    }

    private DataObject CreateXadesObject(XmlDocument document, X509Certificate2 certificate)
    {
        var qualifyingProperties = document.CreateElement("xades", "QualifyingProperties", XadesNamespaceUrl);
        qualifyingProperties.SetAttribute("Target", "signature");

        var signedProperties = document.CreateElement("xades", "SignedProperties", XadesNamespaceUrl);
        signedProperties.SetAttribute("Id", "id-xades-signed-props");

        var signedSignatureProperties = document.CreateElement("xades", "SignedSignatureProperties", XadesNamespaceUrl);

        var signingTime = document.CreateElement("xades", "SigningTime", XadesNamespaceUrl);
        signingTime.InnerText = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        signedSignatureProperties.AppendChild(signingTime);

        var signingCertificate = document.CreateElement("xades", "SigningCertificate", XadesNamespaceUrl);
        var cert = document.CreateElement("xades", "Cert", XadesNamespaceUrl);

        var certDigest = document.CreateElement("xades", "CertDigest", XadesNamespaceUrl);
        var digestMethod = document.CreateElement("ds", "DigestMethod", SignatureNamespaceUrl);
        digestMethod.SetAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#sha256");
        var digestValue = document.CreateElement("ds", "DigestValue", SignatureNamespaceUrl);
        digestValue.InnerText = Convert.ToBase64String(SHA256.HashData(certificate.RawData));
        certDigest.AppendChild(digestMethod);
        certDigest.AppendChild(digestValue);
        cert.AppendChild(certDigest);

        var issuerSerial = document.CreateElement("xades", "IssuerSerial", XadesNamespaceUrl);
        var x509IssuerName = document.CreateElement("ds", "X509IssuerName", SignatureNamespaceUrl);
        x509IssuerName.InnerText = certificate.IssuerName.Name;
        var x509SerialNumber = document.CreateElement("ds", "X509SerialNumber", SignatureNamespaceUrl);
        x509SerialNumber.InnerText = ParseSerialNumber(certificate.SerialNumber);
        issuerSerial.AppendChild(x509IssuerName);
        issuerSerial.AppendChild(x509SerialNumber);
        cert.AppendChild(issuerSerial);

        signingCertificate.AppendChild(cert);
        signedSignatureProperties.AppendChild(signingCertificate);
        signedProperties.AppendChild(signedSignatureProperties);
        qualifyingProperties.AppendChild(signedProperties);

        return new DataObject { Data = qualifyingProperties.SelectNodes(".")! };
    }

    private static string ParseSerialNumber(string hexString)
    {
        var bytes = Convert.FromHexString(hexString);
        Array.Reverse(bytes); 
        return new System.Numerics.BigInteger(bytes, isUnsigned: true).ToString();
    }
}
