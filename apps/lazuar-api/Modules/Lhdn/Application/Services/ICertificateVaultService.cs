using System.Security.Cryptography.X509Certificates;

namespace Modules.Lhdn.Application.Services;

public interface ICertificateVaultService
{
    X509Certificate2 GetDecryptedCertificate(string encryptedPfxBase64, string pfxPasswordCiphertext);
}
