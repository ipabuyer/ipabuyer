using IPAbuyer.Core.Integration.Ipatool;
using Xunit;

namespace IPAbuyer.Tests.Integration.Ipatool
{
    public sealed class IpatoolResultTests
    {
        [Theory]
        [InlineData("output", "error", "output")]
        [InlineData("  output  ", "error", "  output  ")]
        [InlineData(null, "error", "error")]
        [InlineData("", "error", "error")]
        [InlineData("", null, "")]
        public void OutputOrError_PrefersNonBlankOutput(string? output, string? error, string expected)
        {
            var result = new IpatoolResult(output, error, 0, false);

            Assert.Equal(expected, result.OutputOrError);
        }

        [Theory]
        [InlineData(0, false, true)]
        [InlineData(1, false, false)]
        [InlineData(0, true, false)]
        public void IsSuccessResponse_RequiresZeroExitCodeWithoutTimeout(int exitCode, bool timedOut, bool expected)
        {
            var result = new IpatoolResult(null, null, exitCode, timedOut);

            Assert.Equal(expected, result.IsSuccessResponse);
        }
    }
}
