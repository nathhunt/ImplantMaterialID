using System;
using System.Collections.Generic;

namespace ImplantMaterialID.Services
{
    /// <summary>
    /// Patient/structure-set to pre-populate the UI with. Normally empty (the exe was launched
    /// by hand and the user types the patient ID in themselves), but the Eclipse binary plugin
    /// launcher (see the ImplantMaterialID.EclipseLauncher project) supplies these on the command
    /// line so the app opens straight to whatever patient/structure set was already open in
    /// Eclipse, instead of the user having to re-enter the patient ID here.
    /// </summary>
    public sealed class LaunchArguments
    {
        public string PatientId { get; }

        public string StructureSetId { get; }

        public LaunchArguments(string patientId, string structureSetId)
        {
            PatientId = patientId;
            StructureSetId = structureSetId;
        }

        /// <summary>
        /// Parses arguments of the form <c>--patient ID</c> / <c>--patient=ID</c> (also
        /// <c>-p</c>) and <c>--structureset ID</c> / <c>--structureset=ID</c> (also <c>-s</c>),
        /// case-insensitive, values optionally quoted. Both are optional and independent -
        /// either, both, or neither may be present. Unrecognised arguments are ignored, so this
        /// stays forward-compatible with switches added later and tolerant of being started by
        /// hand with no arguments at all, in which case every property comes back null and the
        /// normal manual-entry flow applies untouched.
        /// </summary>
        public static LaunchArguments Parse(IReadOnlyList<string> args)
        {
            string patientId = null;
            string structureSetId = null;

            for (int i = 0; i < args.Count; i++)
            {
                if (TryReadSwitch(args, ref i, "--patient", "-p", out var patientValue))
                {
                    patientId = patientValue;
                    continue;
                }
                if (TryReadSwitch(args, ref i, "--structureset", "-s", out var structureSetValue))
                {
                    structureSetId = structureSetValue;
                    continue;
                }
            }

            return new LaunchArguments(patientId, structureSetId);
        }

        private static bool TryReadSwitch(IReadOnlyList<string> args, ref int i, string longName, string shortName, out string value)
        {
            value = null;
            var arg = args[i];
            if (arg == null)
                return false;

            // --name=value form.
            var eqPrefix = longName + "=";
            if (arg.StartsWith(eqPrefix, StringComparison.OrdinalIgnoreCase))
            {
                value = Unquote(arg.Substring(eqPrefix.Length));
                return true;
            }

            // --name value / -n value form: consumes the following argument as the value.
            if (string.Equals(arg, longName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, shortName, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Count)
                {
                    value = Unquote(args[i + 1]);
                    i++;
                }
                return true;
            }

            return false;
        }

        private static string Unquote(string value)
        {
            if (value != null && value.Length >= 2 && value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal))
                return value.Substring(1, value.Length - 2);
            return value;
        }
    }
}
