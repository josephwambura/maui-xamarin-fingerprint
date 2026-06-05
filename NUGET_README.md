Biometric / Fingerprint plugin for Maui

Maui and MvvMCross plugin for accessing the fingerprint, Face ID or other biometric sensors.

## Setup

### iOS

Add `NSFaceIDUsageDescription` to your Info.plist to describe the reason your app uses Face ID. (see [Documentation](https://developer.apple.com/library/content/documentation/General/Reference/InfoPlistKeyReference/Articles/CocoaKeys.html#//apple_ref/doc/uid/TP40009251-SW75)). Otherwise the App will crash when you start a Face ID authentication on iOS 11.3+.

```xml
<key>NSFaceIDUsageDescription</key>
<string>Need your face to unlock secrets!</string>
```

### Android

**Request the permission in `AndroidManifest.xml`**

```xml
<uses-permission android:name="android.permission.USE_BIOMETRIC" />
<uses-permission android:name="android.permission.USE_FINGERPRINT" />
```

**Set the resolver of the current Activity**
Skip this, if you use the MvvMCross Plugin or don't use the dialog.
MAUI apps have a single Activity. Wire it up in `MainActivity.cs`:
```csharp
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
```

### Windows
Add the capability inside `Package.appxmanifest`:

```xml
<Capabilities>
    <DeviceCapability Name="fingerprint" />
</Capabilities>
```

### MAUI Program Setup
Register the plugin with MAUI’s DI container (skip if using MvvmCross — Plugin.cs `Load()` handles registration):

```chsarp
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
```

## Usage

### Example

#### vanilla

```csharp
var request = new AuthenticationRequestConfiguration ("Prove you have fingers!", "Because without it you can't have access");
var result = await CrossFingerprint.Current.AuthenticateAsync(request);
if (result.Authenticated)
{
    // do secret stuff :)
}
else
{
    // not allowed to do secret stuff :(
}
```

#### using MvvMCross

```csharp
var fpService = Mvx.Resolve<IFingerprint>(); // or use dependency injection and inject IFingerprint

var request = new AuthenticationRequestConfiguration ("Prove you have mvx fingers!", "Because without it you can't have access");
var result = await fpService.AuthenticateAsync(request);
if (result.Authenticated)
{
    // do secret stuff :)
}
else
{
    // not allowed to do secret stuff :(
}
```

### Using MAUI with Dependency Injection
Inject `IFingerprint` into a `ViewModel`:

```csharp
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
            CancelTitle = "Cancel",
            FallbackTitle = "Use PIN",
            AllowAlternativeAuthentication = true,
        };

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
```

## Nice to know

### Android code shrinker (Proguard & r8)

If you use the plugin with Link all, Release Mode and ProGuard/r8 enabled, you may have to do the following:

1. Create a `proguard.cfg` file in your android project and add the following:

```
    -dontwarn com.samsung.**
    -keep class com.samsung.** {*;}
```

2. Include it to your project
3. Properties > Build Action > ProguardConfiguration
