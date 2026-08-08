using IPAbuyer.Core.Integration.Ipatool;
using IPAbuyer.Core.Services.Downloads;
using Xunit;

namespace IPAbuyer.Tests.Services.Downloads
{
    public sealed class DownloadResultParserTests
    {
        [Theory]
        [InlineData("success=true", 1, false, true)]
        [InlineData("success=false", 0, false, false)]
        [InlineData("success=true success=false", 0, false, false)]
        [InlineData("{\"success\":true}", 1, false, true)]
        [InlineData(null, 0, false, true)]
        [InlineData(null, 1, false, false)]
        [InlineData(null, 0, true, false)]
        public void IsSuccess_UsesPayloadStatusOrProcessOutcome(string? output, int exitCode, bool timedOut, bool expected)
        {
            var result = new IpatoolResult(output, null, exitCode, timedOut);

            Assert.Equal(expected, DownloadResultParser.IsSuccess(result));
        }

        [Fact]
        public void GetErrorMessage_ReturnsStructuredErrorDetail()
        {
            var result = new IpatoolResult("{\"error\":\"not authorized\"}", null, 1, false);

            Assert.Equal("not authorized", DownloadResultParser.GetErrorMessage(result));
        }

        [Fact]
        public void GetErrorMessage_TruncatesLongPlainText()
        {
            string output = new('x', 200);
            var result = new IpatoolResult(output, null, 1, false);

            string message = DownloadResultParser.GetErrorMessage(result);

            Assert.Equal(163, message.Length);
            Assert.EndsWith("...", message, StringComparison.Ordinal);
        }
    }
}
