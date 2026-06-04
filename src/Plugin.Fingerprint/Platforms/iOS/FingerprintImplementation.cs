using System;
using System.Threading;
using System.Threading.Tasks;
using Foundation;
using LocalAuthentication;
using ObjCRuntime;
using Plugin.Fingerprint.Abstractions;
#if IOS || MACCATALYST
using UIKit;
#endif

namespace Plugin.Fingerprint
{
    internal class FingerprintImplementation : FingerprintImplementationBase
    {
        private LAContext? _context;

        public FingerprintImplementation()
        {
            CreateLaContext();
        }

        protected override async Task<FingerprintAuthenticationResult> NativeAuthenticateAsync(
            AuthenticationRequestConfiguration authRequestConfig,
            CancellationToken cancellationToken)
        {
            var result = new FingerprintAuthenticationResult();
            SetupContextProperties(authRequestConfig);

            Tuple<bool, NSError?> resTuple;
            using (cancellationToken.Register(CancelAuthentication))
            {
                var policy = GetPolicy(authRequestConfig.AllowAlternativeAuthentication);
                resTuple = await _context.EvaluatePolicyAsync(policy, authRequestConfig.Reason);
            }

            if (resTuple.Item1)
            {
                result.Status = FingerprintAuthenticationResultStatus.Succeeded;
            }
            else
            {
                // #79 simulators return null for any reason
                if (resTuple.Item2 == null)
                {
                    result.Status = FingerprintAuthenticationResultStatus.UnknownError;
                    result.ErrorMessage = "";
                }
                else
                {
                    result = GetResultFromError(resTuple.Item2);
                }
            }

            CreateNewContext();
            return result;
        }

        public override async Task<FingerprintAvailability> GetAvailabilityAsync(bool allowAlternativeAuthentication = false)
        {
            if (_context == null)
                return FingerprintAvailability.NoApi;

            var policy = GetPolicy(allowAlternativeAuthentication);
            if (_context.CanEvaluatePolicy(policy, out var error))
                return FingerprintAvailability.Available;

            return (LAStatus)(int)error.Code switch
            {
                LAStatus.BiometryNotAvailable => IsDeniedError(error)
                    ? FingerprintAvailability.Denied
                    : FingerprintAvailability.NoSensor,
                LAStatus.BiometryNotEnrolled => FingerprintAvailability.NoFingerprint,
                LAStatus.PasscodeNotSet => FingerprintAvailability.NoFallback,
                _ => FingerprintAvailability.Unknown,
            };
        }

        public override async Task<AuthenticationType> GetAuthenticationTypeAsync()
        {
            if (_context == null)
                return AuthenticationType.None;

            // Must call GetAvailabilityAsync first — BiometryType is not populated otherwise
            var availability = await GetAvailabilityAsync(false);

            // iOS/MacCatalyst 11+ — switch directly on LABiometryType, no object cast needed
            if (_context.RespondsToSelector(new Selector("biometryType")))
            {
                return _context.BiometryType switch
                {
                    LABiometryType.None => AuthenticationType.None,
                    LABiometryType.TouchId => AuthenticationType.Fingerprint,
                    LABiometryType.FaceId => AuthenticationType.Face,
                    _ => AuthenticationType.None,
                };
            }

            // Fallback for older OS versions
            if (availability is FingerprintAvailability.NoApi
                             or FingerprintAvailability.NoSensor
                             or FingerprintAvailability.Unknown)
            {
                return AuthenticationType.None;
            }

            return AuthenticationType.Fingerprint;
        }

        private void SetupContextProperties(AuthenticationRequestConfiguration authRequestConfig)
        {
            if (_context.RespondsToSelector(new Selector("localizedFallbackTitle")))
                _context.LocalizedFallbackTitle = authRequestConfig.FallbackTitle;

            if (_context.RespondsToSelector(new Selector("localizedCancelTitle")))
                _context.LocalizedCancelTitle = authRequestConfig.CancelTitle;
        }

        private static LAPolicy GetPolicy(bool allowAlternativeAuthentication)
        {
            return allowAlternativeAuthentication
                ? LAPolicy.DeviceOwnerAuthentication
                : LAPolicy.DeviceOwnerAuthenticationWithBiometrics;
        }

        private static FingerprintAuthenticationResult GetResultFromError(NSError error)
        {
            var result = new FingerprintAuthenticationResult();

            switch ((LAStatus)(int)error.Code)
            {
                case LAStatus.AuthenticationFailed:
                    var description = error.Description;
                    result.Status = description != null && description.Contains("retry limit exceeded")
                        ? FingerprintAuthenticationResultStatus.TooManyAttempts
                        : FingerprintAuthenticationResultStatus.Failed;
                    break;

                case LAStatus.UserCancel:
                case LAStatus.AppCancel:
                    result.Status = FingerprintAuthenticationResultStatus.Canceled;
                    break;

                case LAStatus.UserFallback:
                    result.Status = FingerprintAuthenticationResultStatus.FallbackRequested;
                    break;

                case LAStatus.BiometryLockout:
                    result.Status = FingerprintAuthenticationResultStatus.TooManyAttempts;
                    break;

                case LAStatus.BiometryNotAvailable:
                    result.Status = IsDeniedError(error)
                        ? FingerprintAuthenticationResultStatus.Denied
                        : FingerprintAuthenticationResultStatus.NotAvailable;
                    break;

                default:
                    result.Status = FingerprintAuthenticationResultStatus.UnknownError;
                    break;
            }

            result.ErrorMessage = error.LocalizedDescription;
            return result;
        }

        private void CancelAuthentication() => CreateNewContext();

        private void CreateNewContext()
        {
            if (_context != null)
            {
                if (_context.RespondsToSelector(new Selector("invalidate")))
                    _context.Invalidate();

                _context.Dispose();
            }

            CreateLaContext();
        }

        private void CreateLaContext()
        {
            var info = new NSProcessInfo();

#if MACCATALYST
            // Was #if MACOS — corrected to MACCATALYST to match the net11.0-maccatalyst TFM
            var minVersion = new NSOperatingSystemVersion(10, 12, 0);
            if (!info.IsOperatingSystemAtLeastVersion(minVersion))
                return;
#else
            // SupportedOSPlatformVersion is 15.0, so CheckSystemVersion(8,0) is always
            // true at runtime — kept for clarity but could be removed.
            if (!UIDevice.CurrentDevice.CheckSystemVersion(8, 0))
                return;
#endif

            if (Class.GetHandle(typeof(LAContext)) == IntPtr.Zero)
                return;

            _context = new LAContext();
        }

        private static bool IsDeniedError(NSError error)
        {
            return !string.IsNullOrEmpty(error.Description) &&
                   error.Description.Contains("denied", StringComparison.CurrentCultureIgnoreCase);
        }
    }
}