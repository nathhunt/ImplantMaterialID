using ImplantMaterialID.Services;
using Xunit;

namespace ImplantMaterialID.Tests
{
    public class LaunchArgumentsTests
    {
        [Fact]
        public void NoArguments_LeavesEverythingNull()
        {
            var parsed = LaunchArguments.Parse(new string[0]);

            Assert.Null(parsed.PatientId);
            Assert.Null(parsed.StructureSetId);
        }

        [Theory]
        [InlineData("--patient", "--structureset")]
        [InlineData("-p", "-s")]
        [InlineData("--PATIENT", "--StructureSet")]
        [InlineData("-P", "-S")]
        public void SeparateValueForm_IsParsed(string patientSwitch, string structureSetSwitch)
        {
            var parsed = LaunchArguments.Parse(new[] { patientSwitch, "12345", structureSetSwitch, "CT_1" });

            Assert.Equal("12345", parsed.PatientId);
            Assert.Equal("CT_1", parsed.StructureSetId);
        }

        [Fact]
        public void EqualsForm_IsParsed()
        {
            var parsed = LaunchArguments.Parse(new[] { "--patient=12345", "--STRUCTURESET=CT_1" });

            Assert.Equal("12345", parsed.PatientId);
            Assert.Equal("CT_1", parsed.StructureSetId);
        }

        [Fact]
        public void QuotedValues_AreUnquoted()
        {
            var parsed = LaunchArguments.Parse(new[] { "--patient=\"ID with spaces\"", "-s", "\"CT 1\"" });

            Assert.Equal("ID with spaces", parsed.PatientId);
            Assert.Equal("CT 1", parsed.StructureSetId);
        }

        [Fact]
        public void ValueThatIsOnlyAQuoteCharacter_IsLeftAlone()
        {
            var parsed = LaunchArguments.Parse(new[] { "--patient", "\"" });

            Assert.Equal("\"", parsed.PatientId);
        }

        [Fact]
        public void EitherSwitchMayBeGivenAlone()
        {
            var patientOnly = LaunchArguments.Parse(new[] { "--patient", "12345" });
            var structureSetOnly = LaunchArguments.Parse(new[] { "-s", "CT_1" });

            Assert.Equal("12345", patientOnly.PatientId);
            Assert.Null(patientOnly.StructureSetId);
            Assert.Null(structureSetOnly.PatientId);
            Assert.Equal("CT_1", structureSetOnly.StructureSetId);
        }

        [Fact]
        public void UnrecognisedArgumentsAndNulls_AreIgnored()
        {
            var parsed = LaunchArguments.Parse(new[] { "--verbose", null, "extra", "-p", "12345", "--future=1" });

            Assert.Equal("12345", parsed.PatientId);
            Assert.Null(parsed.StructureSetId);
        }

        [Fact]
        public void SwitchAsLastArgument_HasNoValue()
        {
            var parsed = LaunchArguments.Parse(new[] { "--structureset", "CT_1", "--patient" });

            Assert.Null(parsed.PatientId);
            Assert.Equal("CT_1", parsed.StructureSetId);
        }

        [Fact]
        public void SwitchValueIsNotReparsedAsASwitch()
        {
            // "-s" here is the patient ID, consumed as the value of --patient.
            var parsed = LaunchArguments.Parse(new[] { "--patient", "-s", "--structureset", "CT_1" });

            Assert.Equal("-s", parsed.PatientId);
            Assert.Equal("CT_1", parsed.StructureSetId);
        }

        [Fact]
        public void RepeatedSwitch_LastOneWins()
        {
            var parsed = LaunchArguments.Parse(new[] { "-p", "first", "--patient=second" });

            Assert.Equal("second", parsed.PatientId);
        }

        [Fact]
        public void PrefixOfASwitchName_IsNotMatched()
        {
            var parsed = LaunchArguments.Parse(new[] { "--patientid", "12345", "--pat", "999" });

            Assert.Null(parsed.PatientId);
        }
    }
}
