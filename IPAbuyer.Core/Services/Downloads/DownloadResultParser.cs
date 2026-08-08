using IPAbuyer.Core.Integration.Ipatool;
using IPAbuyer.Core.Serialization;
using Microsoft.Windows.ApplicationModel.Resources;
using System.Text.RegularExpressions;

namespace IPAbuyer.Core.Services.Downloads
{
    internal static class DownloadResultParser
    {
        private static readonly Regex SuccessFlagRegex = new(@"success\s*[:=]\s*(true|false)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static bool IsSuccess(IpatoolResult result)
        {
            string payload = result.OutputOrError;
            if (TryExtractSuccessFlag(payload, out bool success))
            {
                return success;
            }

            if (result.IsSuccessResponse)
            {
                return true;
            }

            return payload.Contains("\"success\":true", StringComparison.OrdinalIgnoreCase)
                || JsonPayload.EnumerateTokens(payload).Any(token => JsonPayload.TryReadBoolean(token, "success", out bool jsonSuccess) && jsonSuccess);
        }

        internal static string GetErrorMessage(IpatoolResult result)
        {
            if (result.TimedOut)
            {
                return GetResourceString("DownloadQueue/Error/Timeout");
            }

            string payload = result.OutputOrError;
            if (string.IsNullOrWhiteSpace(payload))
            {
                return string.Format(System.Globalization.CultureInfo.CurrentCulture, GetResourceString("DownloadQueue/Error/ExitCode"), result.ExitCode);
            }

            foreach (var token in JsonPayload.EnumerateTokens(payload))
            {
                if (JsonPayload.TryReadString(token, out string? detail, "error", "message") && !string.IsNullOrWhiteSpace(detail))
                {
                    return detail;
                }
            }

            return payload.Length > 160 ? payload[..160] + "..." : payload;
        }

        private static string GetResourceString(string key)
        {
            return new ResourceLoader().GetString(key);
        }

        private static bool TryExtractSuccessFlag(string? payload, out bool success)
        {
            success = false;
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            MatchCollection matches = SuccessFlagRegex.Matches(payload);
            if (matches.Count == 0)
            {
                return false;
            }

            success = string.Equals(matches[^1].Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
            return true;
        }
    }
}
