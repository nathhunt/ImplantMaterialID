using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using VMS.TPS.Common.Model.API;

// Namespace/class name are fixed by ESAPI convention for a compiled ("binary") plugin script -
// Eclipse loads this assembly and instantiates VMS.TPS.Script, then calls Execute. Do not rename
// either.
namespace VMS.TPS
{
    /// <summary>
    /// Eclipse binary plugin that launches the standalone Implant Material Identifier executable
    /// (the ImplantMaterialID project/README at the repository root) pre-populated with whatever
    /// patient/structure set is currently open in Eclipse.
    ///
    /// This plugin does no ESAPI computation itself and does not share Eclipse's live
    /// Application/Patient objects with the launched process - that isn't possible across a
    /// process boundary, and isn't needed: it just reads the current patient/structure set IDs
    /// from ScriptContext and passes them as command-line arguments (see LaunchArguments and
    /// MainViewModel.InitializeFromLaunchAsync in the main project) to a new
    /// ImplantMaterialID.exe process, which re-authenticates and re-opens the patient itself via
    /// its own Application.CreateApplication() - typically silent, since it inherits the same
    /// Windows identity already authorised in Eclipse (see the main README's "Login" section).
    /// If no patient is open (or a structure set isn't), the corresponding argument is simply
    /// omitted and the user fills that field in by hand in the launched app, exactly as if it had
    /// been started standalone with no arguments.
    ///
    /// Like the standalone executable, this compiled plugin (or its checksum) generally needs
    /// its own approval under Eclipse > Tools > Script Approval or the equivalent admin tool
    /// before Eclipse will run it - see the main README's deployment checklist. Approving the
    /// .exe does not also approve this DLL, or vice versa; they're separate binaries.
    ///
    /// DEPLOYMENT NOTE: Eclipse's script browser only recognises a compiled binary plugin whose
    /// filename ends in ".esapi.dll" - a plain ".dll" is silently not listed, even in the right
    /// folder. The .csproj's AssemblyName is set accordingly (builds
    /// ImplantMaterialID.EclipseLauncher.esapi.dll) - don't rename that away.
    /// </summary>
    public class Script
    {
        private const string ExecutableFileName = "ImplantMaterialID.exe";

        // Escape hatch for deployments where the plugin DLL and the standalone exe don't live
        // side by side (e.g. the exe is on a shared network path used by every workstation).
        private const string ExecutablePathOverrideEnvironmentVariable = "IMPLANTMATERIALID_EXE_PATH";

        public Script()
        {
        }

        public void Execute(ScriptContext context)
        {
            try
            {
                var exePath = LocateExecutable();
                if (exePath == null)
                {
                    MessageBox.Show(
                        $"Could not find {ExecutableFileName}.\n\n" +
                        "Deploy it in the same folder as this plugin, or set the " +
                        $"{ExecutablePathOverrideEnvironmentVariable} environment variable to its full path.",
                        "Implant Material Identifier",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Only what's actually open in Eclipse right now - either may legitimately be
                // null (no patient open, or a patient but no structure set in context), and
                // that's fine: see class remarks.
                var patientId = context?.Patient?.Id;
                var structureSetId = context?.StructureSet?.Id;

                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = BuildArguments(patientId, structureSetId),
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not launch Implant Material Identifier:\n\n" + ex.Message,
                    "Implant Material Identifier",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Resolves the standalone executable's path: an explicit override via
        /// IMPLANTMATERIALID_EXE_PATH if set and it exists, otherwise ImplantMaterialID.exe
        /// next to this plugin DLL (the recommended deployment layout - see README). Returns
        /// null if neither is found.
        /// </summary>
        private static string LocateExecutable()
        {
            var overridePath = Environment.GetEnvironmentVariable(ExecutablePathOverrideEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
                return overridePath;

            var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (pluginDir == null)
                return null;

            var sideBySide = Path.Combine(pluginDir, ExecutableFileName);
            return File.Exists(sideBySide) ? sideBySide : null;
        }

        private static string BuildArguments(string patientId, string structureSetId)
        {
            var sb = new StringBuilder();
            AppendSwitch(sb, "--patient", patientId);
            AppendSwitch(sb, "--structureset", structureSetId);
            return sb.ToString();
        }

        private static void AppendSwitch(StringBuilder sb, string switchName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (sb.Length > 0)
                sb.Append(' ');

            sb.Append(switchName).Append(' ').Append(Quote(value));
        }

        // Minimal Windows command-line quoting: wrap in quotes and escape embedded quotes.
        // Patient/structure-set IDs are DICOM-style identifiers, so the fuller
        // CommandLineToArgvW backslash-escaping rules aren't needed here.
        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
