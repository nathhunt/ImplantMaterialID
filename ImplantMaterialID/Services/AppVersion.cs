using System;
using System.Reflection;

namespace ImplantMaterialID.Services
{
    /// <summary>
    /// The version this copy of the app was built as, for display. Comes from the assembly's
    /// informational version, which the build sets from VersionPrefix in Directory.Build.props
    /// plus the git commit it was built from (e.g. "1.0.0+44a299ca84ab44ba..."), so it can't
    /// drift from the actual build the way a hand-typed version string in the UI could.
    /// </summary>
    public static class AppVersion
    {
        private const int ShortCommitLength = 7;

        /// <summary>E.g. "1.0.0 (44a299c)", or just "1.0.0" if built without git information.</summary>
        public static string Current { get; } = Describe(typeof(AppVersion).Assembly);

        public static string Describe(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return Format(string.IsNullOrWhiteSpace(informational) ? assembly.GetName().Version?.ToString() : informational);
        }

        /// <summary>
        /// Turns an informational version ("1.0.0+&lt;commit sha&gt;") into a display string
        /// ("1.0.0 (44a299c)"), shortening the commit hash the way git does.
        /// </summary>
        public static string Format(string informationalVersion)
        {
            if (string.IsNullOrWhiteSpace(informationalVersion))
                return "unknown";

            var plus = informationalVersion.IndexOf('+');
            if (plus < 0)
                return informationalVersion.Trim();

            var version = informationalVersion.Substring(0, plus).Trim();
            var metadata = informationalVersion.Substring(plus + 1).Trim();
            if (metadata.Length == 0)
                return version;

            if (metadata.Length > ShortCommitLength && IsHex(metadata))
                metadata = metadata.Substring(0, ShortCommitLength);

            return $"{version} ({metadata})";
        }

        private static bool IsHex(string value)
        {
            foreach (var c in value)
            {
                if (!Uri.IsHexDigit(c))
                    return false;
            }
            return true;
        }
    }
}
