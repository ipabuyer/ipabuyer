using IPAbuyer.Core.Serialization;
using Microsoft.Windows.ApplicationModel.Resources;
using System.Text.RegularExpressions;

namespace IPAbuyer.Core.Integration.Ipatool
{
    internal static class IpatoolResponseParser
    {
        private static readonly Regex EmailRegex = new(
            @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static string ExtractEmail(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return string.Empty;
            }

            foreach (var token in JsonPayload.EnumerateTokens(payload))
            {
                if (JsonPayload.TryReadString(token, out string? email, "email", "eamil") && !string.IsNullOrWhiteSpace(email))
                {
                    return email.Trim();
                }
            }

            Match match = EmailRegex.Match(payload);
            return match.Success ? match.Value : string.Empty;
        }

        internal static bool IsSuccess(string? payload)
        {
            return JsonPayload.EnumerateTokens(payload).Any(token => JsonPayload.TryReadBoolean(token, "success", out bool success) && success)
                || (!string.IsNullOrWhiteSpace(payload)
                    && (payload.Contains("success=true", StringComparison.OrdinalIgnoreCase)
                        || payload.Contains("\"success\":true", StringComparison.OrdinalIgnoreCase)));
        }

        internal static bool HasExplicitFailure(string? payload)
        {
            return JsonPayload.EnumerateTokens(payload).Any(token => JsonPayload.TryReadBoolean(token, "success", out bool success) && !success)
                || (!string.IsNullOrWhiteSpace(payload)
                    && (payload.Contains("success=false", StringComparison.OrdinalIgnoreCase)
                        || payload.Contains("\"success\":false", StringComparison.OrdinalIgnoreCase)));
        }

        internal static bool IsAccountMissingFromKeyring(string? payload)
        {
            return !string.IsNullOrWhiteSpace(payload)
                && payload.Contains("failed to get account", StringComparison.OrdinalIgnoreCase)
                && payload.Contains("could not be found in the keyring", StringComparison.OrdinalIgnoreCase);
        }

        internal static (string Output, string Error) NormalizeStreams(string? stdout, string? stderr, int exitCode)
        {
            string outputText = stdout?.Trim() ?? string.Empty;
            string errorText = stderr?.Trim() ?? string.Empty;
            string normalizedOutput = ExtractMeaningfulJson(outputText) ?? outputText;
            string normalizedError = ExtractMeaningfulJson(errorText) ?? errorText;

            if (string.IsNullOrWhiteSpace(normalizedOutput))
            {
                normalizedOutput = BuildReadableError(normalizedError, exitCode);
            }

            if (string.IsNullOrWhiteSpace(normalizedError) && exitCode != 0)
            {
                normalizedError = BuildReadableError(normalizedOutput, exitCode);
            }

            return (normalizedOutput, normalizedError);
        }

        private static string? ExtractMeaningfulJson(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            string trimmed = content.Trim();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                return trimmed;
            }

            var jsonLines = trimmed.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("{") || line.StartsWith("["));
            string result = string.Join(Environment.NewLine, jsonLines);
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }

        private static string BuildReadableError(string? text, int exitCode)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                string trimmed = text.Trim();
                if (JsonPayload.TryParseToken(trimmed, out var token)
                    && JsonPayload.TryReadString(token, out string? message, "error", "message")
                    && !string.IsNullOrWhiteSpace(message))
                {
                    return string.Format(System.Globalization.CultureInfo.CurrentCulture, GetResourceString("Ipatool/Error/ReadableJsonError"), message, exitCode);
                }

                return trimmed;
            }

            return string.Format(System.Globalization.CultureInfo.CurrentCulture, GetResourceString("Ipatool/Error/ExecutionFailed"), exitCode);
        }

        private static string GetResourceString(string key)
        {
            return new ResourceLoader().GetString(key);
        }
    }
}
