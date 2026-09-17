namespace AgrismartAPI.Application.Abstractions;

public interface IBiometricSignatureVerifier
{
    /// <summary>
    /// Verifies that <paramref name="signatureBase64"/> is a valid signature over <paramref name="challenge"/>
    /// produced by the private key matching <paramref name="publicKeyBase64"/>.
    /// </summary>
    bool Verify(string publicKeyBase64, byte[] challenge, string signatureBase64);
}
