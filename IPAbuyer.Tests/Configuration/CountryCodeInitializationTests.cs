using IPAbuyer.Core.Configuration;
using Xunit;

namespace IPAbuyer.Tests.Configuration
{
    public sealed class CountryCodeInitializationTests
    {
        [Theory]
        [InlineData(null, "cn")]
        [InlineData("", "cn")]
        [InlineData("  ", "cn")]
        [InlineData("ZZ", "cn")]
        [InlineData("unknown", "cn")]
        [InlineData("US", "us")]
        [InlineData(" us ", "us")]
        [InlineData("CN", "cn")]
        [InlineData("Tw", "tw")]
        public void ResolveInitialCountryCode_NormalizesSupportedRegionOrUsesFallback(string? region, string expected)
        {
            Assert.Equal(expected, ConfigurationStore.ResolveInitialCountryCode(region));
        }
    }
}
