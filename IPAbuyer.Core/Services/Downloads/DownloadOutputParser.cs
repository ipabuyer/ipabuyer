using IPAbuyer.Core.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IPAbuyer.Core.Services.Downloads
{
    internal sealed record DownloadOutputUpdate(bool RequestingLicense, IReadOnlyList<string> LogLines);

    internal sealed class DownloadOutputParser
    {
        private static readonly Regex ProgressRegex = new(@"(?<!\d)(\d{1,3}(?:\.\d+)?)\s*[%％]", RegexOptions.Compiled);
        private static readonly Regex AnsiEscapeRegex = new(@"\x1B\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);
        private string _stageBuffer = string.Empty;
        private string _logBuffer = string.Empty;
        private int _lastLoggedPercent = -1;
        private string _lastLog = string.Empty;

        internal DownloadOutputUpdate ProcessChunk(string? chunk)
        {
            if (string.IsNullOrEmpty(chunk))
            {
                return new DownloadOutputUpdate(false, Array.Empty<string>());
            }

            bool requestingLicense = ProcessStageChunk(chunk);
            var logs = ProcessLogChunk(chunk, flush: false);
            return new DownloadOutputUpdate(requestingLicense, logs);
        }

        internal DownloadOutputUpdate Flush()
        {
            bool requestingLicense = ProcessStageChunk("\n");
            return new DownloadOutputUpdate(requestingLicense, ProcessLogChunk("\n", flush: true));
        }

        private bool ProcessStageChunk(string chunk)
        {
            _stageBuffer += Sanitize(chunk);
            if (_stageBuffer.Length > 4096)
            {
                _stageBuffer = _stageBuffer[^4096..];
            }

            bool requestingLicense = false;
            while (TryReadLine(ref _stageBuffer, out string line))
            {
                if (JsonPayload.TryParseToken(line.Trim(), out var token)
                    && JsonPayload.TryReadString(token, out string? message, "message")
                    && string.Equals(message, "purchase", StringComparison.OrdinalIgnoreCase))
                {
                    requestingLicense = true;
                }
            }

            return requestingLicense;
        }

        private IReadOnlyList<string> ProcessLogChunk(string chunk, bool flush)
        {
            _logBuffer += chunk;
            if (_logBuffer.Length > 4096)
            {
                _logBuffer = _logBuffer[^4096..];
            }

            var lines = new List<string>();
            while (TryReadLine(ref _logBuffer, out string line))
            {
                AddIfNew(line, lines);
            }

            if ((flush || _logBuffer.Trim().Length >= 48) && !string.IsNullOrWhiteSpace(_logBuffer))
            {
                AddIfNew(_logBuffer, lines);
                _logBuffer = string.Empty;
            }

            return lines;
        }

        private void AddIfNew(string rawLine, ICollection<string> lines)
        {
            string line = Sanitize(rawLine).Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            int? progress = TryExtractProgress(line);
            if (progress.HasValue)
            {
                if (progress == _lastLoggedPercent)
                {
                    return;
                }

                _lastLoggedPercent = progress.Value;
            }

            if (string.Equals(_lastLog, line, StringComparison.Ordinal))
            {
                return;
            }

            _lastLog = line;
            lines.Add(line);
        }

        private static int? TryExtractProgress(string line)
        {
            MatchCollection matches = ProgressRegex.Matches(line);
            if (matches.Count > 0 && TryConvertProgress(matches[^1].Groups[1].Value, out int percent))
            {
                return percent;
            }

            foreach (var token in JsonPayload.EnumerateTokens(line))
            {
                if (JsonPayload.TryReadString(token, out string? value, "progress", "percent", "percentage", "completed", "completion", "fraction")
                    && value != null
                    && TryConvertProgress(value, out percent))
                {
                    return percent;
                }
            }

            return null;
        }

        private static bool TryConvertProgress(string value, out int percent)
        {
            percent = 0;
            if (!double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                return false;
            }

            if (number >= 0d && number <= 1d)
            {
                number *= 100d;
            }

            percent = Math.Clamp((int)Math.Round(number, MidpointRounding.AwayFromZero), 0, 100);
            return true;
        }

        private static bool TryReadLine(ref string buffer, out string line)
        {
            int index = buffer.IndexOfAny(new[] { '\r', '\n' });
            if (index < 0)
            {
                line = string.Empty;
                return false;
            }

            line = buffer[..index];
            int consume = index + 1 < buffer.Length && buffer[index] == '\r' && buffer[index + 1] == '\n' ? 2 : 1;
            buffer = buffer[(index + consume)..];
            return true;
        }

        private static string Sanitize(string input)
        {
            string withoutAnsi = AnsiEscapeRegex.Replace(input, string.Empty);
            var chars = new char[withoutAnsi.Length];
            int index = 0;
            foreach (char ch in withoutAnsi)
            {
                if (ch == '\r' || ch == '\n' || ch == '\t' || !char.IsControl(ch))
                {
                    chars[index++] = ch;
                }
            }

            return index == 0 ? string.Empty : new string(chars, 0, index);
        }
    }
}
