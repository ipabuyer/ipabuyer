using IPAbuyer.Core.Configuration;
using System.Text.RegularExpressions;

namespace IPAbuyer.Core.Integration.Ipatool
{
    internal static class IpatoolCommandLog
    {
        private const int MaxPreviewLength = 200;
        private static readonly HashSet<string> SensitiveSwitches = new(StringComparer.OrdinalIgnoreCase)
        {
            "--password",
            "--auth-code",
            "--keychain-passphrase"
        };
        private static readonly Regex SensitiveJsonPropertyRegex = new(
            "(\"(?:password|authCode|keychainPassphrase|keychain-passphrase|keychain_passphrase|passphrase|PasswordToken)\"\\s*:\\s*)\"(?:\\\\.|[^\"])*\"",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static void EmitCommandIfEnabled(IReadOnlyList<string> arguments, Action<string>? commandSink)
        {
            if (ApplicationSettings.GetDetailedIpatoolLogEnabled())
            {
                commandSink?.Invoke($"ipatool {RenderArgumentsForDisplay(arguments)}");
            }
        }

        internal static void EmitOutputIfEnabled(string? output, string? error, Action<string>? outputSink)
        {
            if (!ApplicationSettings.GetDetailedIpatoolLogEnabled())
            {
                return;
            }

            foreach (string line in EnumerateLines(output).Concat(EnumerateLines(error)))
            {
                outputSink?.Invoke(Sanitize(line));
            }
        }

        internal static string Preview(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            value = Sanitize(value);
            return value.Length <= MaxPreviewLength ? value : value[..MaxPreviewLength] + "...";
        }

        internal static string Sanitize(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : SensitiveJsonPropertyRegex.Replace(value, "$1\"***\"");
        }

        private static IEnumerable<string> EnumerateLines(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            foreach (string rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    yield return line;
                }
            }
        }

        private static string RenderArgumentsForDisplay(IReadOnlyList<string> arguments)
        {
            var rendered = new List<string>(arguments.Count);
            for (int i = 0; i < arguments.Count; i++)
            {
                string argument = arguments[i];
                rendered.Add(FormatArgumentForDisplay(argument));
                if (SensitiveSwitches.Contains(argument) && i + 1 < arguments.Count)
                {
                    rendered.Add("\"***\"");
                    i++;
                }
            }

            return string.Join(" ", rendered);
        }

        private static string FormatArgumentForDisplay(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return "\"\"";
            }

            return !argument.Any(char.IsWhiteSpace) && !argument.Contains('"')
                ? argument
                : "\"" + argument.Replace("\"", "\\\"") + "\"";
        }
    }
}
