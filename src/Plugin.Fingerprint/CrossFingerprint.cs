using System;
using System.Threading;

using Plugin.Fingerprint.Abstractions;
#if ANDROID
using Plugin.Fingerprint.Contract;
#endif

namespace Plugin.Fingerprint
{
    /// <summary>
    /// Cross Platform Fingerprint.
    /// </summary>
    public partial class CrossFingerprint
    {
        private static Lazy<IFingerprint> _implementation = new Lazy<IFingerprint>(CreateFingerprint, LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// Current plugin implementation to use
        /// </summary>
        public static IFingerprint Current
        {
            get => _implementation.Value;
            set
            {
                _implementation = new Lazy<IFingerprint>(() => value);
            }
        }

        static IFingerprint CreateFingerprint()
        {
            // All four supported TFMs (Android, iOS, MacCatalyst, Windows) have a
            // concrete FingerprintImplementation. The old NETSTANDARD2_0 guard was
            // for the reference-assembly fallback TFM which no longer exists.
            return new FingerprintImplementation();
        }

        /// <summary>
        /// Cleans up implementation reference.
        /// </summary>
        public static void Dispose()
        {
            if (_implementation != null && _implementation.IsValueCreated)
            {
                _implementation = new Lazy<IFingerprint>(CreateFingerprint, LazyThreadSafetyMode.PublicationOnly);
            }
        }
    }
}