using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OperatorPhone
{
    /// <summary>
    /// Lets the mod ship as a single DLL by embedding its managed dependencies as
    /// resources and resolving them on demand, instead of requiring the user to place
    /// files in the game's UserLibs folder.
    ///
    /// Timing is the whole trick. The CLR resolves an assembly the first time a method
    /// referencing one of its types is JIT-compiled — which for PhotonClient is the
    /// first call to ChatService.Pump(). [ModuleInitializer] runs immediately after this
    /// assembly loads and before any of our code executes, so the handler is always
    /// installed in time. Registering from OnInitializeMelon would also work today, but
    /// only by luck of call ordering; this is guaranteed.
    /// </summary>
    internal static class EmbeddedAssemblies
    {
        private static bool _installed;

        [ModuleInitializer]
        internal static void Install()
        {
            if (_installed) return;
            _installed = true;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            // args.Name is a full display name ("PhotonClient, Version=5.1.17.0, ...").
            // Match on the simple name only: the embedded copy is whatever we built
            // against, and failing over a version-field mismatch would be pointless.
            var simpleName = new AssemblyName(args.Name).Name;
            if (string.IsNullOrEmpty(simpleName)) return null;

            var resourceName = "OperatorPhone.Embedded." + simpleName + ".dll";
            var self = typeof(EmbeddedAssemblies).Assembly;

            using (var stream = self.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return null; // not ours; let other handlers try

                var bytes = new byte[stream.Length];
                var read = 0;
                while (read < bytes.Length)
                {
                    var n = stream.Read(bytes, read, bytes.Length - read);
                    if (n <= 0) break;
                    read += n;
                }

                try
                {
                    var loaded = Assembly.Load(bytes);
                    Console.WriteLine($"[OperatorPhone] Loaded embedded assembly: {simpleName}");
                    return loaded;
                }
                catch (Exception e)
                {
                    // MelonLogger may not exist yet at module-init time, so this
                    // deliberately uses Console rather than PhoneMod.Log.
                    Console.WriteLine($"[OperatorPhone] Failed to load embedded {simpleName}: {e}");
                    return null;
                }
            }
        }
    }
}
