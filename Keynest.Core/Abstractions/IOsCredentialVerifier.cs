namespace Keynest.Core.Abstractions;

/// <summary>
/// Platform-specific OS credential (biometric/PIN) verifier.
/// Windows: implement via UserConsentVerifier.
/// macOS: implement via LocalAuthentication.
/// Linux: implement via polkit or libfprint.
/// </summary>
public interface IOsCredentialVerifier
{
    /// <summary>
    /// Returns true if the user verified, false if denied.
    /// Must return true (graceful fallback) if biometrics are unavailable or unsupported.
    /// </summary>
    Task<bool> VerifyAsync(string message);
}
