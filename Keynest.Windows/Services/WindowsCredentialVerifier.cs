using Keynest.Core.Abstractions;
using Windows.Security.Credentials.UI;

namespace Keynest.Windows.Services;

public sealed class WindowsCredentialVerifier : IOsCredentialVerifier
{
    public async Task<bool> VerifyAsync(string message)
    {
        try
        {
            var availability = await UserConsentVerifier.CheckAvailabilityAsync();
            if (availability != UserConsentVerifierAvailability.Available)
                return true; // graceful fallback — hardware not present

            var result = await UserConsentVerifier.RequestVerificationAsync(message);
            return result == UserConsentVerificationResult.Verified;
        }
        catch
        {
            return true; // graceful fallback
        }
    }
}
