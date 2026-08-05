using System.Reflection;
using MelonLoader;
using OperatorPhone;

[assembly: AssemblyTitle(PhoneBuildInfo.Name)]
[assembly: AssemblyDescription(PhoneBuildInfo.Description)]
[assembly: AssemblyProduct(PhoneBuildInfo.Name)]
[assembly: AssemblyVersion(PhoneBuildInfo.Version)]
[assembly: AssemblyFileVersion(PhoneBuildInfo.Version)]

[assembly: MelonInfo(typeof(PhoneMod), PhoneBuildInfo.Name, PhoneBuildInfo.Version, PhoneBuildInfo.Author)]
[assembly: MelonColor(255, 90, 200, 255)]

namespace OperatorPhone
{
    /// <summary>
    /// Named PhoneBuildInfo, not BuildInfo: MelonLoader exposes its own BuildInfo type
    /// and `using MelonLoader;` makes the short name ambiguous.
    /// </summary>
    internal static class PhoneBuildInfo
    {
        public const string Name = "OperatorPhone";
        public const string Description = "In-game smartphone: messaging, media, links.";
        public const string Author = "remero";
        public const string Version = "0.1.0";
    }
}