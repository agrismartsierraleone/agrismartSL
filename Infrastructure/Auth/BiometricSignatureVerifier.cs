using System.Security.Cryptography;
using AgrismartAPI.Application.Abstractions;

namespace AgrismartAPI.Infrastructure.Auth;

/// <summary>
/// Verifies ECDSA (P-256, SHA-256, DER-encoded signature) proofs produced by the mobile app's
/// biometric-gated key pair. This matches the native signature format produced by Android
/// Keystore ("SHA256withECDSA") and iOS Secure Enclave (ecdsaSignatureMessageX962SHA256).
/// </summary>
public class BiometricSignatureVerifier : IBiometricSignatureVerifier
{
    public bool Verify(string publicKeyBase64, byte[] challenge, string signatureBase64)
    {
        try
        {
            var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
            var signatureBytes = Convert.FromBase64String(signatureBase64);

            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

            return ecdsa.VerifyData(challenge, signatureBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return false;
        }
    }
}
