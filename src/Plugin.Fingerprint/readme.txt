---------------------------------
Maui and MvvmCross plugin for authenticating a user via fingerprint, face id or any other biometic / local authentication method from a cross platform API.
---------------------------------

How to setup:
Read carefully (especially the Android X part)! https://github.com/smstuebe/xamarin-fingerprint

Star on Github at: https://github.com/smstuebe/xamarin-fingerprint

Feel free to buy me a bavarian beer: https://www.paypal.me/smstuebe

Usage:

// ──────────────────────────────────────────────────────────────────────────────
// 1. ANDROID — Platforms/Android/AndroidManifest.xml
//    Add both permissions inside <manifest>
// ──────────────────────────────────────────────────────────────────────────────

/*
<uses-permission android:name="android.permission.USE_BIOMETRIC" />
<uses-permission android:name="android.permission.USE_FINGERPRINT" />
*/


// ──────────────────────────────────────────────────────────────────────────────
// 2. ANDROID — Platforms/Android/MainActivity.cs
//    Tell the plugin how to reach the current Activity.
//    MAUI apps have a single Activity — wire it up in OnCreate.
// ──────────────────────────────────────────────────────────────────────────────

using Android.App;
using Android.OS;
using Microsoft.Maui;
using Plugin.Fingerprint;

namespace YourApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Required — the plugin uses the Activity to show BiometricPrompt.
        CrossFingerprint.SetCurrentActivityResolver(() => this);
    }
}


// ──────────────────────────────────────────────────────────────────────────────
// 3. iOS — Platforms/iOS/Info.plist
//    Face ID requires a usage description or the OS will crash the app.
// ──────────────────────────────────────────────────────────────────────────────

/*
<key>NSFaceIDUsageDescription</key>
<string>We use Face ID to authenticate you securely.</string>
*/


// ──────────────────────────────────────────────────────────────────────────────
// 4. WINDOWS — Platforms/Windows/Package.appxmanifest
//    Add the capability inside <Capabilities> so UserConsentVerifier works.
// ──────────────────────────────────────────────────────────────────────────────

/*
<DeviceCapability Name="fingerprint" />
*/


// ──────────────────────────────────────────────────────────────────────────────
// 5. MauiProgram.cs — register the plugin with MAUI's DI container
//    (Skip if using MvvmCross — the Plugin.cs Load() handles registration.)
// ──────────────────────────────────────────────────────────────────────────────

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

namespace YourApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Register IFingerprint as a singleton so it can be injected into ViewModels.
        builder.Services.AddSingleton<IFingerprint>(_ => CrossFingerprint.Current);

        return builder.Build();
    }
}


// ──────────────────────────────────────────────────────────────────────────────
// 6. USAGE — inject IFingerprint into a ViewModel or use CrossFingerprint directly
// ──────────────────────────────────────────────────────────────────────────────

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Fingerprint.Abstractions;

namespace YourApp.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IFingerprint _fingerprint;

    public LoginViewModel(IFingerprint fingerprint)
    {
        _fingerprint = fingerprint;
    }

    [RelayCommand]
    async Task AuthenticateAsync()
    {
        var config = new AuthenticationRequestConfiguration(
            title:  "Verify your identity",
            reason: "Authenticate to access your account")
        {
            CancelTitle                  = "Cancel",
            FallbackTitle                = "Use PIN",
            AllowAlternativeAuthentication = true,  // allows PIN/password fallback
        };

        // Check availability before prompting (optional but recommended)
        var availability = await _fingerprint.GetAvailabilityAsync(
            allowAlternativeAuthentication: config.AllowAlternativeAuthentication);

        if (availability != FingerprintAvailability.Available)
        {
            await Shell.Current.DisplayAlert("Unavailable",
                $"Biometric auth not available: {availability}", "OK");
            return;
        }

        var result = await _fingerprint.AuthenticateAsync(config);

        if (result.Authenticated)
        {
            // success — navigate or unlock
        }
        else
        {
            await Shell.Current.DisplayAlert("Failed",
                result.ErrorMessage ?? result.Status.ToString(), "OK");
        }
    }
}