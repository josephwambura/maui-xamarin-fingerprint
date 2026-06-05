using System;
using System.Collections.Generic;
using Plugin.Fingerprint.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content.PM;
using Android.Runtime;
using AndroidX.Biometric;
using AndroidX.Fragment.App;
using AndroidX.Lifecycle;
using Java.Util.Concurrent;
using System.Linq;

namespace Plugin.Fingerprint
{
    /// <summary>
    /// Android fingerprint implementations.
    /// </summary>
    public class FingerprintImplementation : FingerprintImplementationBase
    {
        private readonly BiometricManager _manager;

        public FingerprintImplementation()
        {
            _manager = BiometricManager.From(Application.Context);
        }

        public override async Task<AuthenticationType> GetAuthenticationTypeAsync()
        {
            var availability = await GetAvailabilityAsync(false);
            if (availability == FingerprintAvailability.NoFingerprint ||
                availability == FingerprintAvailability.NoPermission ||
                availability == FingerprintAvailability.Available)
            {
                return AuthenticationType.Fingerprint;
            }

            return AuthenticationType.None;
        }

        public override async Task<FingerprintAvailability> GetAvailabilityAsync(bool allowAlternativeAuthentication = false)
        {
            var biometricAvailability = GetBiometricAvailability();
            if (biometricAvailability == FingerprintAvailability.Available || !allowAlternativeAuthentication)
                return biometricAvailability;

            var context = Application.Context;

            try
            {
                var manager = (KeyguardManager?)context.GetSystemService(Android.Content.Context.KeyguardService);
                if (manager?.IsDeviceSecure == true)
                {
                    return FingerprintAvailability.Available;
                }

                return FingerprintAvailability.NoFallback;
            }
            catch
            {
                return FingerprintAvailability.NoFallback;
            }
        }

        private FingerprintAvailability GetBiometricAvailability()
        {
            if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.M)
                return FingerprintAvailability.NoApi;

            var context = Application.Context;

            if (context.CheckCallingOrSelfPermission(Manifest.Permission.UseBiometric) != Permission.Granted &&
                context.CheckCallingOrSelfPermission(Manifest.Permission.UseFingerprint) != Permission.Granted)
                return FingerprintAvailability.NoPermission;

            var result = _manager.CanAuthenticate(BiometricManager.Authenticators.BiometricStrong |
                                                   BiometricManager.Authenticators.BiometricWeak);

            return result switch
            {
                BiometricManager.BiometricErrorNoHardware => FingerprintAvailability.NoSensor,
                BiometricManager.BiometricErrorHwUnavailable => FingerprintAvailability.Unknown,
                BiometricManager.BiometricErrorNoneEnrolled => FingerprintAvailability.NoFingerprint,
                BiometricManager.BiometricSuccess => FingerprintAvailability.Available,
                _ => FingerprintAvailability.Unknown,
            };
        }

        protected override async Task<FingerprintAuthenticationResult> NativeAuthenticateAsync(
            AuthenticationRequestConfiguration authRequestConfig,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(authRequestConfig.Title))
                throw new ArgumentException("Title must not be null or empty on Android.", nameof(authRequestConfig.Title));

            if (CrossFingerprint.CurrentActivity is not FragmentActivity)
                throw new InvalidOperationException(
                    $"Expected current activity to be '{typeof(FragmentActivity).FullName}' but was " +
                    $"'{CrossFingerprint.CurrentActivity?.GetType().FullName}'. " +
                    "You need to use AndroidX.");

            try
            {
                var cancel = string.IsNullOrWhiteSpace(authRequestConfig.CancelTitle)
                    ? Application.Context.GetString(Android.Resource.String.Cancel)
                    : authRequestConfig.CancelTitle;

                var handler = new AuthenticationHandler();
                var builder = new BiometricPrompt.PromptInfo.Builder()
                    .SetTitle(authRequestConfig.Title)
                    .SetConfirmationRequired(authRequestConfig.ConfirmationRequired)
                    .SetDescription(authRequestConfig.Reason);

                if (authRequestConfig.AllowAlternativeAuthentication)
                {
                    builder = builder.SetAllowedAuthenticators(
                        BiometricManager.Authenticators.BiometricStrong |
                        BiometricManager.Authenticators.BiometricWeak |
                        BiometricManager.Authenticators.DeviceCredential);
                }
                else
                {
                    builder = builder
                        .SetAllowedAuthenticators(
                            BiometricManager.Authenticators.BiometricStrong |
                            BiometricManager.Authenticators.BiometricWeak)
                        .SetNegativeButtonText(cancel);
                }

                var info = builder.Build();
                var executor = Executors.NewSingleThreadExecutor();

                var activity = (FragmentActivity)CrossFingerprint.CurrentActivity;
                using var dialog = new BiometricPrompt(activity, executor, handler);
                await using (cancellationToken.Register(() => dialog.CancelAuthentication()))
                {
                    dialog.Authenticate(info);
                    var result = await handler.GetTask();

                    TryReleaseLifecycleObserver(activity, dialog);

                    return result;
                }
            }
            catch (Exception e)
            {
                return new FingerprintAuthenticationResult
                {
                    Status = FingerprintAuthenticationResultStatus.UnknownError,
                    ErrorMessage = e.Message
                };
            }
        }

        /// <summary>
        /// Removes the lifecycle observer set by BiometricPrompt from the lifecycle owner.
        /// See: https://stackoverflow.com/a/59637670/1489968
        /// TODO: Recheck after AndroidX.Biometric is updated — newer versions may not need this.
        /// </summary>
        private static void TryReleaseLifecycleObserver(ILifecycleOwner lifecycleOwner, BiometricPrompt dialog)
        {
            var promptClass = Java.Lang.Class.FromType(dialog.GetType());
            var fields = promptClass.GetDeclaredFields();
            var lifecycleObserverField = fields?.FirstOrDefault(f => f.Name == "mLifecycleObserver");

            if (lifecycleObserverField is null)
                return;

            lifecycleObserverField.Accessible = true;
            var lastLifecycleObserver = lifecycleObserverField.Get(dialog).JavaCast<ILifecycleObserver>();
            var lifecycle = lifecycleOwner.Lifecycle;

            if (lastLifecycleObserver is null || lifecycle is null)
                return;

            lifecycle.RemoveObserver(lastLifecycleObserver);
        }
    }
}