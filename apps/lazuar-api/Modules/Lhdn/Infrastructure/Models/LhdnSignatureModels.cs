using System.Text.Json.Serialization;
using Modules.Lhdn.Infrastructure.Serialization;

namespace Modules.Lhdn.Infrastructure.Models;

public record LhdnUblExtension(
    [property: JsonPropertyOrder(1)] UblValue<string> ExtensionURI,
    [property: JsonPropertyOrder(2)] LhdnExtensionContent[] ExtensionContent
);

public record LhdnExtensionContent(
    [property: JsonPropertyOrder(1)] LhdnUblDocumentSignatures[] UBLDocumentSignatures
);

public record LhdnUblDocumentSignatures(
    [property: JsonPropertyOrder(1)] LhdnSignatureInformation[] SignatureInformation
);

public record LhdnSignatureInformation(
    [property: JsonPropertyOrder(1)] UblValue<string> ID,
    [property: JsonPropertyOrder(2)] UblValue<string> ReferencedSignatureID,
    [property: JsonPropertyOrder(3)] LhdnSignature[] Signature
);

public record LhdnSignature(
    [property: JsonPropertyOrder(1)] string Id,
    [property: JsonPropertyOrder(2)] LhdnSignatureObject[] Object,
    [property: JsonPropertyOrder(3)] LhdnKeyInfo[] KeyInfo,
    [property: JsonPropertyOrder(4)] UblValue<string> SignatureValue,
    [property: JsonPropertyOrder(5)] LhdnSignedInfo[] SignedInfo
);

public record LhdnSignatureObject(
    [property: JsonPropertyOrder(1)] LhdnQualifyingProperties[] QualifyingProperties
);

public record LhdnQualifyingProperties(
    [property: JsonPropertyOrder(1)] string Target,
    [property: JsonPropertyOrder(2)] LhdnSignedProperties[] SignedProperties
);

public record LhdnSignedProperties(
    [property: JsonPropertyOrder(1)] string Id,
    [property: JsonPropertyOrder(2)] LhdnSignedSignatureProperties[] SignedSignatureProperties
);

public record LhdnSignedSignatureProperties(
    [property: JsonPropertyOrder(1)] UblValue<string> SigningTime,
    [property: JsonPropertyOrder(2)] LhdnSigningCertificate[] SigningCertificate
);

public record LhdnSigningCertificate(
    [property: JsonPropertyOrder(1)] LhdnCert[] Cert
);

public record LhdnCert(
    [property: JsonPropertyOrder(1)] LhdnCertDigest[] CertDigest,
    [property: JsonPropertyOrder(2)] LhdnIssuerSerial[] IssuerSerial
);

public record LhdnCertDigest(
    [property: JsonPropertyOrder(1)] LhdnDigestMethod[] DigestMethod,
    [property: JsonPropertyOrder(2)] UblValue<string> DigestValue
);

public record LhdnDigestMethod(
    [property: JsonPropertyName("_")] string Value,
    [property: JsonPropertyName("Algorithm")] string Algorithm
);

public record LhdnIssuerSerial(
    [property: JsonPropertyOrder(1)] UblValue<string> X509IssuerName,
    [property: JsonPropertyOrder(2)] UblValue<string> X509SerialNumber
);

public record LhdnKeyInfo(
    [property: JsonPropertyOrder(1)] LhdnX509Data[] X509Data
);

public record LhdnX509Data(
    [property: JsonPropertyOrder(1)] UblValue<string> X509Certificate,
    [property: JsonPropertyOrder(2)] UblValue<string> X509SubjectName,
    [property: JsonPropertyOrder(3)] LhdnIssuerSerial[] X509IssuerSerial
);

public record LhdnSignedInfo(
    [property: JsonPropertyOrder(1)] LhdnSignatureMethod[] SignatureMethod,
    [property: JsonPropertyOrder(2)] LhdnReference[] Reference
);

public record LhdnSignatureMethod(
    [property: JsonPropertyName("_")] string Value,
    [property: JsonPropertyName("Algorithm")] string Algorithm
);

public record LhdnReference(
    [property: JsonPropertyOrder(1)] string Type,
    [property: JsonPropertyOrder(2)] string URI,
    [property: JsonPropertyOrder(3)] LhdnDigestMethod[] DigestMethod,
    [property: JsonPropertyOrder(4)] UblValue<string> DigestValue
);

public record LhdnSignatureReference(
    [property: JsonPropertyOrder(1)] UblValue<string> ID,
    [property: JsonPropertyOrder(2)] UblValue<string> SignatureMethod
);
