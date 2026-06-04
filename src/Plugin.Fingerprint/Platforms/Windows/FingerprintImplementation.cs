// Moved from Platforms\UWP → Platforms\Windows to match the csproj TFM (net11.0-windows).
// Windows.Security.Credentials.UI.UserConsentVerifier is available in WinUI 3 /
// Windows App SDK without API changes — the namespace is identical to UWP.

using System;
using System.Threading;
using System.Threading.Tasks;

using Windows.Security.Credentials.UI;

using Plugin.Fingerprint.Abstractions;

namespace Plugin.Fingerprint
{
    internal class FingerprintImplementation : FingerprintImplementationBase
    {
        protected override async Task<FingerprintAuthenticationResult> NativeAuthenticateAsync(
            AuthenticationRequestConfiguration authRequestConfig,
            CancellationToken cancellationToken = default)
        {
            var result = new FingerprintAuthenticationResult();

            try
            {
                var verificationResult = await UserConsentVerifier.RequestVerificationAsync(authRequestConfig.Reason);

                result.Status = verificationResult switch
                {
                    UserConsentVerificationResult.Verified => FingerprintAuthenticationResultStatus.Succeeded,
                    UserConsentVerificationResult.DeviceBusy
                        or UserConsentVerificationResult.DeviceNotPresent
                        or UserConsentVerificationResult.DisabledByPolicy
                        or UserConsentVerificationResult.NotConfiguredForUser
                        => FingerprintAuthenticationResultStatus.NotAvailable,
                    UserConsentVerificationResult.RetriesExhausted => FingerprintAuthenticationResultStatus.TooManyAttempts,
                    UserConsentVerificationResult.Canceled => FingerprintAuthenticationResultStatus.Canceled,
                    _ => FingerprintAuthenticationResultStatus.Failed,
                };
            }
            catch (Exception ex)
            {
                result.Status = FingerprintAuthenticationResultStatus.UnknownError;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public override async Task<FingerprintAvailability> GetAvailabilityAsync(bool allowAlternativeAuthentication = false)
        {
            var availability = await UserConsentVerifier.CheckAvailabilityAsync();

            return availability switch
            {
                UserConsentVerifierAvailability.Available => FingerprintAvailability.Available,
                UserConsentVerifierAvailability.DeviceNotPresent => FingerprintAvailability.NoSensor,
                UserConsentVerifierAvailability.NotConfiguredForUser => FingerprintAvailability.NoFingerprint,
                UserConsentVerifierAvailability.DisabledByPolicy => FingerprintAvailability.NoPermission,
                _ => FingerprintAvailability.Unknown,
            };
        }

        public override async Task<AuthenticationType> GetAuthenticationTypeAsync()
        {
            var availability = await GetAvailabilityAsync(false);
            if (availability is FingerprintAvailability.NoFingerprint
                             or FingerprintAvailability.NoPermission
                             or FingerprintAvailability.Available)
            {
                return AuthenticationType.Fingerprint;
            }

            return AuthenticationType.None;
        }
    }
}