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

        [Fact]
        public void ProcessChunk_HandlesWindowsLineEndings()
        {
            var parser = new DownloadOutputParser();

            DownloadOutputUpdate update = parser.ProcessChunk("first line\r\nsecond line\r\n");

            Assert.Equal(new[] { "first line", "second line" }, update.LogLines);
        }

        [Fact]
        public void ProcessChunk_SuppressesConsecutiveIdenticalLines()
        {
            var parser = new DownloadOutputParser();

            _ = parser.ProcessChunk("same line\n");
            DownloadOutputUpdate update = parser.ProcessChunk("same line\n");

            Assert.Empty(update.LogLines);
        }

        [Fact]
        public void ProcessChunk_ExtractsProgressFromJsonFields()
        {
            var parser = new DownloadOutputParser();

            DownloadOutputUpdate update = parser.ProcessChunk("{\"progress\":\"0.5\"}\n");

            Assert.Equal(new[] { "{\"progress\":\"0.5\"}" }, update.LogLines);
        }

        [Fact]
        public void ProcessChunk_ConvertsFractionalJsonProgressToPercent()
        {
            var parser = new DownloadOutputParser();

            DownloadOutputUpdate first = parser.ProcessChunk("{\"completed\":\"0.25\"}\n");
            DownloadOutputUpdate second = parser.ProcessChunk("{\"completed\":\"0.75\"}\n");

            Assert.Single(first.LogLines);
            Assert.Single(second.LogLines);
        }

        [Fact]
        public void ProcessChunk_ClampsOutOfRangeJsonProgress()
        {
            var parser = new DownloadOutputParser();

            DownloadOutputUpdate first = parser.ProcessChunk("{\"percent\":250}\n");
            DownloadOutputUpdate second = parser.ProcessChunk("Cap 100%\n");

            Assert.Single(first.LogLines);
            Assert.Empty(second.LogLines);
        }

        [Fact]
        public void ProcessChunk_SuppressesSameProgressAcrossTextAndJsonFormats()
        {
            var parser = new DownloadOutputParser();

            DownloadOutputUpdate first = parser.ProcessChunk("{\"progress\":\"0.5\"}\n");
            DownloadOutputUpdate second = parser.ProcessChunk("Halfway there 50%\n");

            Assert.Single(first.LogLines);
            Assert.Empty(second.LogLines);
        }

        [Fact]
        public void Flush_OnFreshParserProducesNoLines()
        {
            var parser = new DownloadOutputParser();

            DownloadOutputUpdate update = parser.Flush();

            Assert.False(update.RequestingLicense);
            Assert.Empty(update.LogLines);
        }
    }
}
