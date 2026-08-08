using IPAbuyer.Core.Services.Downloads;
using Xunit;

namespace IPAbuyer.Tests.Services.Downloads
{
    public sealed class DownloadOutputParserTests
    {
        [Fact]
        public void ProcessChunk_RecognizesPurchaseAfterSplitJsonLineCompletes()
        {
            var parser = new DownloadOutputParser();

            DownloadOutputUpdate first = parser.ProcessChunk("{\"message\":\"pur");
            DownloadOutputUpdate second = parser.ProcessChunk("chase\"}\n");

            Assert.False(first.RequestingLicense);
            Assert.True(second.RequestingLicense);
        }

        [Fact]
        public void Flush_RecognizesUnterminatedPurchaseMessage()
        {
            var parser = new DownloadOutputParser();
            _ = parser.ProcessChunk("{\"message\":\"purchase\"}");

            DownloadOutputUpdate update = parser.Flush();

            Assert.True(update.RequestingLicense);
        }

        [Fact]
        public void ProcessChunk_SanitizesAnsiAndSuppressesDuplicateProgress()
        {
            var parser = new DownloadOutputParser();

            DownloadOutputUpdate first = parser.ProcessChunk("\u001b[31mDownloading 50%\u001b[0m\n");
            DownloadOutputUpdate second = parser.ProcessChunk("Still downloading 50%\n");

            Assert.Equal(new[] { "Downloading 50%" }, first.LogLines);
            Assert.Empty(second.LogLines);
        }

        [Fact]
        public void ProcessChunk_UsesLastProgressValueAndAcceptsFullWidthPercent()
        {
            var parser = new DownloadOutputParser();

            DownloadOutputUpdate update = parser.ProcessChunk("Progress 5% then 25％\n");

            Assert.Equal(new[] { "Progress 5% then 25％" }, update.LogLines);
        }

        [Fact]
        public void Flush_EmitsBufferedPartialLine()
        {
            var parser = new DownloadOutputParser();
            _ = parser.ProcessChunk("final output");

            DownloadOutputUpdate update = parser.Flush();

            Assert.Equal(new[] { "final output" }, update.LogLines);
        }

        [Fact]
        public void ProcessChunk_EmptyChunkProducesNoUpdate()
        {
            var parser = new DownloadOutputParser();

            DownloadOutputUpdate update = parser.ProcessChunk(string.Empty);

            Assert.False(update.RequestingLicense);
            Assert.Empty(update.LogLines);
        }
    }
}
