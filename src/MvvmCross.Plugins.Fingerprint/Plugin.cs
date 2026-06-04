using System.Diagnostics.CodeAnalysis;
using MvvmCross.IoC;
using MvvmCross.Plugin;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

namespace MvvmCross.Plugins.Fingerprint
{
    [MvxPlugin]
    // [Preserve(AllMembers = true)] was a Foundation (Apple-only) attribute.
    // DynamicallyAccessedMembers is the cross-platform .NET replacement — it
    // tells the linker to keep all members of this class on all platforms.
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public class Plugin : IMvxPlugin
    {
        public void Load(IMvxIoCProvider provider)
        {
            // Mvx.LazyConstructAndRegisterSingleton is deprecated.
            // Mvx.IoCProvider is the current API; IFingerprint type parameter
            // is required so the container knows which interface to resolve.
            provider.RegisterSingleton<IFingerprint>(() => CrossFingerprint.Current);
        }
    }
}