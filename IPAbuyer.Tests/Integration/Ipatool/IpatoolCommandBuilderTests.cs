using IPAbuyer.Core.Integration.Ipatool;
using Xunit;

namespace IPAbuyer.Tests.Integration.Ipatool
{
    public sealed class IpatoolCommandBuilderTests
    {
        [Theory]
        [InlineData("auth", "revoke", true)]
        [InlineData("AUTH", "REVOKE", true)]
        [InlineData("auth", "login", false)]
        [InlineData("purchase", "revoke", false)]
        public void IsLogout_RecognizesOnlyAuthRevoke(string first, string second, bool expected)
        {
            Assert.Equal(expected, IpatoolCommandBuilder.IsLogout(new[] { first, second }));
        }

        [Fact]
        public void IsLogout_RejectsShortArgumentLists()
        {
            Assert.False(IpatoolCommandBuilder.IsLogout(Array.Empty<string>()));
            Assert.False(IpatoolCommandBuilder.IsLogout(new[] { "revoke" }));
        }

        [Fact]
        public void BuildStandardArguments_AppendsPassphraseAndGlobalSwitches()
        {
            IReadOnlyList<string> arguments = IpatoolCommandBuilder.BuildStandardArguments(
                new[] { "auth", "info" },
                "secret",
                isLogout: false);

            Assert.Equal(
                new[] { "auth", "info", "--keychain-passphrase", "secret", "--format", "json", "--non-interactive", "--verbose" },
                arguments);
        }

        [Fact]
        public void BuildStandardArguments_OmitsPassphraseForLogout()
        {
            IReadOnlyList<string> arguments = IpatoolCommandBuilder.BuildStandardArguments(
                new[] { "auth", "revoke" },
                "secret",
                isLogout: true);

            Assert.Equal(
                new[] { "auth", "revoke", "--format", "json", "--non-interactive", "--verbose" },
                arguments);
        }

        [Fact]
        public void BuildDownloadArguments_BuildsCompleteCommand()
        {
            IReadOnlyList<string> arguments = IpatoolCommandBuilder.BuildDownloadArguments(
                "com.example.app",
                @"C:\Downloads",
                "secret");

            Assert.Equal(
                new[]
                {
                    "download", "--output", @"C:\Downloads", "--bundle-identifier", "com.example.app", "--purchase",
                    "--keychain-passphrase", "secret", "--format", "json", "--non-interactive", "--verbose"
                },
                arguments);
        }

        [Fact]
        public void CreateEnvironmentVariables_DisablesColorAndInteractiveTerm()
        {
            IReadOnlyDictionary<string, string> variables = IpatoolCommandBuilder.CreateEnvironmentVariables();

            Assert.Equal(2, variables.Count);
            Assert.Equal("1", variables["NO_COLOR"]);
            Assert.Equal("dumb", variables["TERM"]);
        }

        [Theory]
        [InlineData(new string[0], "ipatool")]
        [InlineData(new[] { "auth" }, "ipatool auth")]
        [InlineData(new[] { "auth", "info" }, "ipatool auth info")]
        [InlineData(new[] { "auth", "info", "--verbose" }, "ipatool auth info")]
        public void GetSafeCommandLabel_UsesAtMostTwoArguments(string[] arguments, string expected)
        {
            Assert.Equal(expected, IpatoolCommandBuilder.GetSafeCommandLabel(arguments));
        }
    }
}
