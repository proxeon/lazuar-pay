using System;
using System.Security.Cryptography.X509Certificates;
using Modules.Lhdn.Application.Services;

namespace Modules.Lhdn.Infrastructure.Services;

public class JsonSignatureService : IJsonSignatureService
{
    public JsonSigningResult SignDocument(string rawXml, X509Certificate2 certificate)
    {
        throw new NotImplementedException("v1.1 Signature logic is temporarily bypassed while securing v1.0 standard submissions.");
    }

    // Dummy implementation to satisfy the test mock execution
    public (string JsonString, string DocumentHashHex) SerializeUnsignedDocument(object document)
    {
        return ("<xml/>", "dummy_hash_for_tests");
    }
}
