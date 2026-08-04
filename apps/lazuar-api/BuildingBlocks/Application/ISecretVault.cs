namespace BuildingBlocks.Application;

/// <summary>
/// AES-based secret vault for encrypting tenant credentials at rest (Resend keys, etc.).
/// Mirrors LHDN certificate vault crypto shape: IV prepended to ciphertext, base64 stored.
/// </summary>
public interface ISecretVault
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertextBase64);
}
