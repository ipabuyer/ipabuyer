using IPAbuyer.Core.Integration.Ipatool;
using Xunit;

namespace IPAbuyer.Tests.Integration.Ipatool
{
    public sealed class IpatoolResponseParserTests
    {
        [Theory]
        [InlineData("{\"email\":\" user@example.com \"}", "user@example.com")]
        [InlineData("{\"eamil\":\"legacy@example.com\"}", "legacy@example.com")]
        [InlineData("Account: prose@example.com", "prose@example.com")]
        [InlineData(null, "")]
        [InlineData("no account present", "")]
        public void ExtractEmail_ReadsJsonAliasesAndTextFallback(string? payload, string expected)
        {
            Assert.Equal(expected, IpatoolClient.ExtractEmailFromPayload(payload));
        }

        [Theory]
        [InlineData("{\"success\":true}")]
        [InlineData("{\"success\":\"true\"}")]
        [InlineData("{\"success\":1}")]
        [InlineData("debug\n{\"success\":true}")]
        [InlineData("success=true")]
        public void IsPayloadSuccess_RecognizesSuccessFormats(string payload)
        {
            Assert.True(IpatoolClient.IsPayloadSuccess(payload));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("{\"success\":false}")]
        [InlineData("success=false")]
        [InlineData("no status")]
        public void IsPayloadSuccess_RejectsNonSuccessPayloads(string? payload)
        {
            Assert.False(IpatoolClient.IsPayloadSuccess(payload));
        }

        [Theory]
        [InlineData("{\"success\":false}")]
        [InlineData("{\"success\":\"false\"}")]
        [InlineData("{\"success\":0}")]
        [InlineData("success=false")]
        public void HasExplicitFailureFlag_RecognizesFailureFormats(string payload)
        {
            Assert.True(IpatoolClient.HasExplicitFailureFlag(payload));
        }

        [Theory]
        [InlineData("failed to get account: the item could not be found in the keyring")]
        [InlineData("FAILED TO GET ACCOUNT; COULD NOT BE FOUND IN THE KEYRING")]
        public void IsAccountMissingFromKeyring_RequiresBothKnownPhrases(string payload)
        {
            Assert.True(IpatoolClient.IsAccountMissingFromKeyring(payload));
        }

        [Theory]
        [InlineData("failed to get account")]
        [InlineData("could not be found in the keyring")]
        [InlineData("other error")]
        public void IsAccountMissingFromKeyring_RejectsPartialOrUnrelatedMessages(string payload)
        {
            Assert.False(IpatoolClient.IsAccountMissingFromKeyring(payload));
        }

        [Fact]
        public void NormalizeStreams_PreservesJsonOutputAndUsesStderrWhenOutputIsEmpty()
        {
            (string output, string error) jsonResult = IpatoolResponseParser.NormalizeStreams("  {\"success\":true}  ", null, 0);
            (string output, string error) errorResult = IpatoolResponseParser.NormalizeStreams(null, "permission denied", 1);

            Assert.Equal("{\"success\":true}", jsonResult.output);
            Assert.Equal("permission denied", errorResult.output);
        }

        [Fact]
        public void NormalizeStreams_ExtractsJsonLinesFromNoisyOutput()
        {
            (string output, string error) result = IpatoolResponseParser.NormalizeStreams("debug\n{\"success\":true}\ntrace", null, 0);

            Assert.Equal("{\"success\":true}", result.output);
        }

        [Fact]
        public void NormalizeStreams_PreservesPlainTextOutputWithoutJson()
        {
            (string output, string error) result = IpatoolResponseParser.NormalizeStreams("plain output", null, 0);

            Assert.Equal("plain output", result.output);
            Assert.Equal(string.Empty, result.error);
        }

        [Fact]
        public void NormalizeStreams_ExtractsJsonLinesFromNoisyErrorStream()
        {
            (string output, string error) result = IpatoolResponseParser.NormalizeStreams("{\"a\":1}", "debug\n{\"b\":2}", 0);

            Assert.Equal("{\"a\":1}", result.output);
            Assert.Equal("{\"b\":2}", result.error);
        }
    }
}
