using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Infrastructure.Models;
using Modules.Lhdn.Infrastructure.Serialization;

namespace Modules.Lhdn.Infrastructure.Services;

public class JsonSignatureService : IJsonSignatureService
{
    private const string ExtensionUri = "urn:oasis:names:specification:ubl:dsig:enveloped:xades";
    private const string SignatureId = "urn:oasis:names:specification:ubl:signature:1";
    private const string ReferencedSignatureId = "urn:oasis:names:specification:ubl:signature:Invoice";
    private const string SignatureMethodAlgo = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    private const string DigestMethodAlgo = "http://www.w3.org/2001/04/xmlenc#sha256";

    public JsonSigningResult SignDocument(object document, X509Certificate2 certificate)
    {
        var typedDocument = document as LhdnJsonDocument ?? throw new ArgumentException("Provided document is not a valid LhdnJsonDocument.", nameof(document));
        var signingTime = DateTime.UtcNow;

        var documentHashBytes = HashDocument(typedDocument);
        var documentBase64Digest = Convert.ToBase64String(documentHashBytes);

        var certDigestBase64 = Convert.ToBase64String(SHA256.HashData(certificate.RawData));
        var certContent = Convert.ToBase64String(certificate.RawData);
        var issuerName = ParseDistinguishedName(certificate.Issuer);
        var subjectName = ParseDistinguishedName(certificate.Subject);
        var serialNumber = ParseSerialNumber(certificate.SerialNumber);
        var formattedSigningTime = signingTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var qualifyingProperties = new LhdnQualifyingProperties(
            Target: "signature",
            SignedProperties: new[]
            {
                new LhdnSignedProperties(
                    Id: "id-xades-signed-props",
                    SignedSignatureProperties: new[]
                    {
                        new LhdnSignedSignatureProperties(
                            SigningTime: formattedSigningTime,
                            SigningCertificate: new[]
                            {
                                new LhdnSigningCertificate(
                                    Cert: new[]
                                    {
                                        new LhdnCert(
                                            CertDigest: new[]
                                            {
                                                new LhdnCertDigest(
                                                    DigestMethod: new[] { new LhdnDigestMethod("", DigestMethodAlgo) },
                                                    DigestValue: certDigestBase64
                                                )
                                            },
                                            IssuerSerial: new[]
                                            {
                                                new LhdnIssuerSerial(issuerName, serialNumber)
                                            }
                                        )
                                    }
                                )
                            }
                        )
                    }
                )
            }
        );

        var propsHashBytes = HashDocument(qualifyingProperties);
        var propsBase64Digest = Convert.ToBase64String(propsHashBytes);

        var rsa = certificate.GetRSAPrivateKey() ?? throw new InvalidOperationException("Certificate does not contain an RSA private key.");
        
#pragma warning disable CA1416 
        var signatureBytes = rsa.SignData(documentHashBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
#pragma warning restore CA1416
        
        var signatureValue = Convert.ToBase64String(signatureBytes);

        var ublExtensions = new[]
        {
            new LhdnUblExtension(
                ExtensionURI: ExtensionUri,
                ExtensionContent: new[]
                {
                    new LhdnExtensionContent(
                        UBLDocumentSignatures: new[]
                        {
                            new LhdnUblDocumentSignatures(
                                SignatureInformation: new[]
                                {
                                    new LhdnSignatureInformation(
                                        ID: SignatureId,
                                        ReferencedSignatureID: ReferencedSignatureId,
                                        Signature: new[]
                                        {
                                            new LhdnSignature(
                                                Id: "signature",
                                                Object: new[] { new LhdnSignatureObject(new[] { qualifyingProperties }) },
                                                KeyInfo: new[]
                                                {
                                                    new LhdnKeyInfo(
                                                        X509Data: new[]
                                                        {
                                                            new LhdnX509Data(
                                                                X509Certificate: certContent,
                                                                X509SubjectName: subjectName,
                                                                X509IssuerSerial: new[] { new LhdnIssuerSerial(issuerName, serialNumber) }
                                                            )
                                                        }
                                                    )
                                                },
                                                SignatureValue: signatureValue,
                                                SignedInfo: new[]
                                                {
                                                    new LhdnSignedInfo(
                                                        SignatureMethod: new[] { new LhdnSignatureMethod("", SignatureMethodAlgo) },
                                                        Reference: new[]
                                                        {
                                                            new LhdnReference(
                                                                Type: "http://uri.etsi.org/01903/v1.3.2#SignedProperties",
                                                                URI: "#id-xades-signed-props",
                                                                DigestMethod: new[] { new LhdnDigestMethod("", DigestMethodAlgo) },
                                                                DigestValue: propsBase64Digest
                                                            ),
                                                            new LhdnReference(
                                                                Type: "",
                                                                URI: "",
                                                                DigestMethod: new[] { new LhdnDigestMethod("", DigestMethodAlgo) },
                                                                DigestValue: documentBase64Digest
                                                            )
                                                        }
                                                    )
                                                }
                                            )
                                        }
                                    )
                                }
                            )
                        }
                    )
                }
            )
        };

        var signatureReference = new LhdnSignatureReference(ReferencedSignatureId, ExtensionUri);

        var signedInvoice = typedDocument.Invoice[0] with
        {
            UBLExtensions = ublExtensions,
            Signature = new object[] { signatureReference }
        };

        var finalDocument = typedDocument with { Invoice = new[] { signedInvoice } };
        var finalJsonString = JsonSerializer.Serialize(finalDocument, LhdnJsonOptions.Instance);

        // Compute the SHA256 hash of the completely assembled and signed document string to match API submission requirements
        var finalHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(finalJsonString));
        var finalDocumentHexDigest = Convert.ToHexString(finalHashBytes).ToLowerInvariant();

        return new JsonSigningResult(
            FinalJsonString: finalJsonString,
            HexDigest: finalDocumentHexDigest,
            SignatureValue: signatureValue
        );
    }

    public (string JsonString, string DocumentHashHex) SerializeUnsignedDocument(object document)
    {
        var finalJsonString = JsonSerializer.Serialize(document, LhdnJsonOptions.Instance);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(finalJsonString));
        var documentHashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return (finalJsonString, documentHashHex);
    }

    private static byte[] HashDocument<T>(T document)
    {
        using var memoryStream = new MemoryStream();
        JsonSerializer.Serialize(memoryStream, document, LhdnJsonOptions.Instance);
        memoryStream.Position = 0;
        return SHA256.HashData(memoryStream);
    }

    private static string ParseDistinguishedName(string dn)
    {
        var parts = dn.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
        Array.Reverse(parts);
        return string.Join(", ", parts);
    }

    private static string ParseSerialNumber(string hexString)
    {
        var bytes = Convert.FromHexString(hexString);
        Array.Reverse(bytes);
        return new System.Numerics.BigInteger(bytes, isUnsigned: true).ToString();
    }
}
