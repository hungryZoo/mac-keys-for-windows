using System.Reflection;

namespace MacKeys;

internal static class AppRelease
{
    public static string Current
    {
        get
        {
            var raw = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            return ReleaseVersion.TryParse(raw, out var version) ? version.ToString() : "0.0.0";
        }
    }
}
