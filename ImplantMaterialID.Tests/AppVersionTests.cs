using System;
using System.Reflection;
using ImplantMaterialID.Services;
using Xunit;

namespace ImplantMaterialID.Tests
{
    public class AppVersionTests
    {
        [Theory]
        [InlineData("1.0.0+44a299ca84ab44ba75015e8b136aef3479f8f319", "1.0.0 (44a299c)")]
        [InlineData("1.2.3-rc1+44A299CA84AB44BA75015E8B136AEF3479F8F319", "1.2.3-rc1 (44A299C)")]
        [InlineData("1.0.0+44a299c", "1.0.0 (44a299c)")]
        [InlineData("1.0.0+ci.42", "1.0.0 (ci.42)")]
        [InlineData("1.0.0+", "1.0.0")]
        [InlineData("1.0.0", "1.0.0")]
        [InlineData(" 1.0.0 ", "1.0.0")]
        [InlineData("1.0.0.0", "1.0.0.0")]
        [InlineData("", "unknown")]
        [InlineData(null, "unknown")]
        public void Format_ShortensCommitHashLikeGit(string informationalVersion, string expected)
        {
            Assert.Equal(expected, AppVersion.Format(informationalVersion));
        }

        [Fact]
        public void Current_ComesFromTheBuild()
        {
            // AppVersion.cs is compiled into this test assembly, so Current describes it - and
            // it gets its version from the same Directory.Build.props as the app.
            var informational = typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            var assemblyVersion = typeof(AppVersion).Assembly.GetName().Version;

            Assert.Equal(AppVersion.Format(informational), AppVersion.Current);
            Assert.StartsWith($"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}", AppVersion.Current);
            Assert.NotEqual("0.0.0.0", assemblyVersion.ToString());
        }

        [Fact]
        public void Describe_NullAssembly_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => AppVersion.Describe(null));
        }
    }
}
