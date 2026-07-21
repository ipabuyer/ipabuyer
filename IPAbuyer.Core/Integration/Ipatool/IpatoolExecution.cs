using IPAbuyer.Core.Configuration;
using IPAbuyer.Core.Execution;
using Microsoft.Windows.ApplicationModel.Resources;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace IPAbuyer.Core.Integration.Ipatool
{
    public static class IpatoolExecution
    {
        private enum IpatoolFlavor
        {
            Main,
            Custom
        }

        private static readonly ResourceLoader Loader = new();
        private static readonly ProcessExecutionService ProcessExecutionService = new();
        private const int MaxPreviewLength = 200;
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);
        private static readonly HttpClient HttpClient = new();
        private static readonly HashSet<string> SensitiveSwitches = new(StringComparer.OrdinalIgnoreCase)
        {
            "--password",
            "--auth-code",
            "--keychain-passphrase"
        };
        private static readonly Regex EmailRegex = new(
            @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SensitiveJsonPropertyRegex = new(
            "(\"(?:password|authCode|keychainPassphrase|keychain-passphrase|keychain_passphrase|passphrase|PasswordToken)\"\\s*:\\s*)\"(?:\\\\.|[^\"])*\"",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public sealed record IpatoolResult(string? Output, string? Error, int ExitCode, bool TimedOut)
        {
            public string OutputOrError => string.IsNullOrWhiteSpace(Output) ? Error ?? string.Empty : Output;
            public bool IsSuccessResponse => !TimedOut && ExitCode == 0;
        }

        public static event Action<string>? CommandExecuting;
        public static event Action<string>? CommandOutputReceived;

        public static void BeginShutdown()
        {
            ProcessExecutionService.BeginShutdown();
        }

        public static Task<IpatoolResult> AuthLoginAsync(string account, string password, string authCode, string passphrase, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(account))
            {
                throw new ArgumentException(LF("Ipatool/Error/RequiredArgument", "account"), nameof(account));
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException(LF("Ipatool/Error/RequiredArgument", "password"), nameof(password));
            }

            var arguments = new List<string>
            {
                "auth",
                "login",
                "--email",
                account,
                "--password",
                password
            };
            if (!string.IsNullOrWhiteSpace(authCode))
            {
                arguments.Add("--auth-code");
                arguments.Add(authCode);
            }

            return ExecuteIpatoolAsync(arguments, account, passphrase, cancellationToken);
        }

        public static Task<IpatoolResult> AuthLogoutAsync(CancellationToken cancellationToken = default)
        {
            return ExecuteIpatoolAsync(new[] { "auth", "revoke" }, account: string.Empty, passphrase: null, cancellationToken);
        }

        public static async Task<IpatoolResult> SearchAppAsync(string name, int limit, string account, string countryCode, CancellationToken cancellationToken = default)
        {
            string safeName = name?.Trim() ?? string.Empty;
            if (safeName.Length == 0)
            {
                throw new ArgumentException(L("Ipatool/Error/AppNameRequired"), nameof(name));
            }

            int cappedLimit = Math.Max(1, limit);
            string normalizedCountry = string.IsNullOrWhiteSpace(countryCode)
                ? "cn"
                : countryCode.Trim().ToLowerInvariant();

            string requestUri = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(safeName)}&entity=software&limit={cappedLimit}&country={Uri.EscapeDataString(normalizedCountry)}";

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(DefaultTimeout);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                request.Headers.TryAddWithoutValidation("User-Agent", "IPAbuyer/1.0");

                using HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, linkedCts.Token).ConfigureAwait(false);
                string content = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = LF("Ipatool/Error/HttpRequestFailed", (int)response.StatusCode, response.ReasonPhrase ?? string.Empty);
                    return new IpatoolResult(null, string.IsNullOrWhiteSpace(content) ? errorMessage : content, (int)response.StatusCode, TimedOut: false);
                }

                return new IpatoolResult(content, null, ExitCode: 0, TimedOut: false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new IpatoolResult(null, LF("Ipatool/Error/ExecutionTimeout", requestUri), ExitCode: -1, TimedOut: true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new IpatoolResult(null, ex.Message, ExitCode: -1, TimedOut: false);
            }
        }

        public static Task<IpatoolResult> AuthInfoAsync(string? passphrase = null, CancellationToken cancellationToken = default, bool silent = false)
        {
            return ExecuteIpatoolAsync(
                new[] { "auth", "info" },
                account: string.Empty,
                passphrase: passphrase,
                cancellationToken,
                suppressLogEvents: silent);
        }

        public static string ExtractEmailFromPayload(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return string.Empty;
            }

            foreach (var token in JsonPayload.EnumerateTokens(payload))
            {
                if (JsonPayload.TryReadString(token, out string? email, "email", "eamil")
                    && !string.IsNullOrWhiteSpace(email))
                {
                    return email.Trim();
                }
            }

            Match match = EmailRegex.Match(payload);
            return match.Success ? match.Value : string.Empty;
        }

        public static bool IsPayloadSuccess(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            foreach (var token in JsonPayload.EnumerateTokens(payload))
            {
                if (JsonPayload.TryReadBoolean(token, "success", out bool success) && success)
                {
                    return true;
                }
            }

            return payload.Contains("success=true", StringComparison.OrdinalIgnoreCase)
                || payload.Contains("\"success\":true", StringComparison.OrdinalIgnoreCase);
        }

        public static bool HasExplicitFailureFlag(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            foreach (var token in JsonPayload.EnumerateTokens(payload))
            {
                if (JsonPayload.TryReadBoolean(token, "success", out bool success) && !success)
                {
                    return true;
                }
            }

            return payload.Contains("success=false", StringComparison.OrdinalIgnoreCase)
                || payload.Contains("\"success\":false", StringComparison.OrdinalIgnoreCase);
        }

        public static Task<IpatoolResult> PurchaseAppAsync(string bundleId, string account, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(account))
            {
                throw new ArgumentException(LF("Ipatool/Error/RequiredArgument", "account"), nameof(account));
            }

            if (string.IsNullOrWhiteSpace(bundleId))
            {
                throw new ArgumentException(LF("Ipatool/Error/RequiredArgument", "bundleId"), nameof(bundleId));
            }

            return ExecuteIpatoolAsync(
                new[] { "purchase", "--bundle-identifier", bundleId },
                account,
                null,
                cancellationToken);
        }

        public static Task<IpatoolResult> DownloadAppAsync(string bundleId, string outputDirectory, string account, CancellationToken cancellationToken = default)
        {
            return DownloadAppWithProgressAsync(bundleId, outputDirectory, account, null, cancellationToken);
        }

        public static async Task<IpatoolResult> DownloadAppWithProgressAsync(
            string bundleId,
            string outputDirectory,
            string account,
            Action<string>? outputChunkCallback,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(account))
            {
                throw new ArgumentException(LF("Ipatool/Error/RequiredArgument", "account"), nameof(account));
            }

            if (string.IsNullOrWhiteSpace(bundleId))
            {
                throw new ArgumentException(LF("Ipatool/Error/RequiredArgument", "bundleId"), nameof(bundleId));
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException(LF("Ipatool/Error/RequiredArgument", "outputDirectory"), nameof(outputDirectory));
            }

            Directory.CreateDirectory(outputDirectory);

            string ipatoolPath = ResolveIpatoolPath();
            string workingDirectory = Path.GetDirectoryName(ipatoolPath) ?? AppContext.BaseDirectory;
            string effectivePassphrase = EnsurePassphrase(null);
            var finalArguments = new List<string>
            {
                "download", "--output", outputDirectory, "--bundle-identifier", bundleId, "--purchase",
                "--keychain-passphrase", effectivePassphrase, "--format", "json", "--non-interactive", "--verbose"
            };

            try
            {
                EmitCommandLog(ipatoolPath, finalArguments);
                var request = new ProcessExecutionRequest(
                    ipatoolPath,
                    workingDirectory,
                    finalArguments,
                    DefaultTimeout,
                    CreateIpatoolEnvironmentVariables(),
                    outputChunkCallback);
                ProcessExecutionResult result = await ProcessExecutionService.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

                if (result.TimedOut)
                {
                    return new IpatoolResult(null, LF("Ipatool/Error/ExecutionTimeout", $"download --bundle-identifier {bundleId}"), ExitCode: -1, TimedOut: true);
                }

                if (outputChunkCallback == null)
                {
                    EmitDetailedOutputLogs(result.StandardOutput, result.StandardError);
                }

                Debug.WriteLine($"ipatool output: {Preview(result.StandardOutput)}");
                Debug.WriteLine($"ipatool stderr: {Preview(result.StandardError)}");
                (string normalizedOutput, string normalizedError) = NormalizeIpatoolStreams(result.StandardOutput, result.StandardError, result.ExitCode);
                return new IpatoolResult(normalizedOutput, normalizedError, result.ExitCode, TimedOut: false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new IpatoolResult(null, ex.Message, ExitCode: -1, TimedOut: false);
            }
        }

        private static async Task<IpatoolResult> ExecuteIpatoolAsync(
            IReadOnlyList<string> arguments,
            string account,
            string? passphrase,
            CancellationToken cancellationToken,
            bool suppressLogEvents = false)
        {
            bool isLogout = arguments.Count >= 2
                && string.Equals(arguments[0], "auth", StringComparison.OrdinalIgnoreCase)
                && string.Equals(arguments[1], "revoke", StringComparison.OrdinalIgnoreCase);

            string ipatoolPath = ResolveIpatoolPath();
            string workingDirectory = Path.GetDirectoryName(ipatoolPath) ?? AppContext.BaseDirectory;

            string effectivePassphrase = EnsurePassphrase(passphrase);

            var finalArguments = new List<string>(arguments.Count + 6);
            foreach (string arg in arguments)
            {
                finalArguments.Add(arg);
            }

            if (!isLogout)
            {
                finalArguments.Add("--keychain-passphrase");
                finalArguments.Add(effectivePassphrase);
            }

            finalArguments.Add("--format");
            finalArguments.Add("json");
            finalArguments.Add("--non-interactive");
            finalArguments.Add("--verbose");

            try
            {
                if (isLogout)
                {
                    DeleteCookieLockFile();
                }

                if (!suppressLogEvents)
                {
                    EmitCommandLog(ipatoolPath, finalArguments);
                }

                var request = new ProcessExecutionRequest(
                    ipatoolPath,
                    workingDirectory,
                    finalArguments,
                    DefaultTimeout,
                    CreateIpatoolEnvironmentVariables());
                ProcessExecutionResult result = await ProcessExecutionService.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
                if (result.TimedOut)
                {
                    return new IpatoolResult(null, LF("Ipatool/Error/ExecutionTimeout", GetSafeCommandLabel(arguments)), ExitCode: -1, TimedOut: true);
                }

                if (!suppressLogEvents)
                {
                    EmitDetailedOutputLogs(result.StandardOutput, result.StandardError);
                }

                Debug.WriteLine($"ipatool output: {Preview(result.StandardOutput)}");
                Debug.WriteLine($"ipatool stderr: {Preview(result.StandardError)}");
                (string normalizedOutput, string normalizedError) = NormalizeIpatoolStreams(result.StandardOutput, result.StandardError, result.ExitCode);
                return new IpatoolResult(normalizedOutput, normalizedError, result.ExitCode, TimedOut: false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new IpatoolResult(null, ex.Message, -1, TimedOut: false);
            }
            finally
            {
                if (isLogout)
                {
                    DeleteCookieLockFile();
                }
            }
        }

        private static string ResolveIpatoolPath()
        {
            if (GetConfiguredIpatoolFlavor() == IpatoolFlavor.Custom)
            {
                string customPath = KeychainConfig.GetCustomIpatoolPath();
                if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
                {
                    return customPath;
                }
            }

            string baseDirectory = AppContext.BaseDirectory;
            string defaultPath = Path.Combine(baseDirectory, "ipatool.exe");
            if (File.Exists(defaultPath))
            {
                return defaultPath;
            }

            string includeDirectory = Path.Combine(baseDirectory, "Include");
            if (Directory.Exists(includeDirectory))
            {
                string architectureSuffix = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.Arm64 => "arm64",
                    Architecture.X64 => "amd64",
                    _ => string.Empty
                };

                if (!string.IsNullOrEmpty(architectureSuffix))
                {
                    string candidate = Path.Combine(includeDirectory, $"ipatool-2.3.1-windows-{architectureSuffix}.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            Debug.WriteLine(L("Ipatool/Debug/FallbackToPath"));
            return "ipatool.exe";
        }

        private static IpatoolFlavor GetConfiguredIpatoolFlavor()
        {
            return string.Equals(KeychainConfig.GetIpatoolFlavor(), KeychainConfig.IpatoolFlavorCustom, StringComparison.OrdinalIgnoreCase)
                && KeychainConfig.HasUsableCustomIpatoolPath()
                ? IpatoolFlavor.Custom
                : IpatoolFlavor.Main;
        }

        private static string EnsurePassphrase(string? passphrase)
        {
            if (!string.IsNullOrWhiteSpace(passphrase))
            {
                return passphrase.Trim();
            }

            string? storedPassphrase = KeychainConfig.GetPassphrase(null);
            if (!string.IsNullOrWhiteSpace(storedPassphrase))
            {
                return storedPassphrase;
            }

            throw new InvalidOperationException(L("Ipatool/Error/MissingPassphrase"));
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

            string[] lines = trimmed.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var jsonLines = new List<string>();
            foreach (string line in lines)
            {
                string candidate = line.Trim();
                if (candidate.StartsWith("{") || candidate.StartsWith("["))
                {
                    jsonLines.Add(candidate);
                }
            }

            if (jsonLines.Count > 0)
            {
                return string.Join(Environment.NewLine, jsonLines);
            }

            return null;
        }

        private static (string Output, string Error) NormalizeIpatoolStreams(string? stdout, string? stderr, int exitCode)
        {
            string outputText = stdout?.Trim() ?? string.Empty;
            string errorText = stderr?.Trim() ?? string.Empty;

            string? outputJson = ExtractMeaningfulJson(outputText);
            string? errorJson = ExtractMeaningfulJson(errorText);

            string normalizedOutput = !string.IsNullOrWhiteSpace(outputJson) ? outputJson : outputText;
            string normalizedError = !string.IsNullOrWhiteSpace(errorJson) ? errorJson : errorText;

            if (string.IsNullOrWhiteSpace(normalizedError) && !string.IsNullOrWhiteSpace(errorText))
            {
                normalizedError = errorText;
            }

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

        private static string BuildReadableError(string? text, int exitCode)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                string trimmed = text.Trim();
                if (JsonPayload.TryParseToken(trimmed, out var token))
                {
                    if (JsonPayload.TryReadString(token, out string? error, "error") && !string.IsNullOrWhiteSpace(error))
                    {
                        return LF("Ipatool/Error/ReadableJsonError", error, exitCode);
                    }

                    if (JsonPayload.TryReadString(token, out string? message, "message") && !string.IsNullOrWhiteSpace(message))
                    {
                        return LF("Ipatool/Error/ReadableJsonError", message, exitCode);
                    }
                }

                return trimmed;
            }

            return LF("Ipatool/Error/ExecutionFailed", exitCode);
        }

        private static string Preview(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            value = SanitizeLogText(value);
            return value.Length <= MaxPreviewLength ? value : value.Substring(0, MaxPreviewLength) + "...";
        }

        private static void DeleteCookieLockFile()
        {
            try
            {
                string lockPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ipatool", "cookies.lock");
                if (File.Exists(lockPath))
                {
                    File.Delete(lockPath);
                    Debug.WriteLine(LF("Ipatool/Debug/DeleteCookieLock", lockPath));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(LF("Ipatool/Debug/DeleteCookieLockFailed", ex.Message));
            }
        }

        private static IReadOnlyDictionary<string, string> CreateIpatoolEnvironmentVariables()
        {
            return new Dictionary<string, string>
            {
                ["NO_COLOR"] = "1",
                ["TERM"] = "dumb"
            };
        }

        private static string GetSafeCommandLabel(IReadOnlyList<string> arguments)
        {
            if (arguments.Count == 0)
            {
                return "ipatool";
            }

            if (arguments.Count == 1)
            {
                return $"ipatool {arguments[0]}";
            }

            return $"ipatool {arguments[0]} {arguments[1]}";
        }

        private static void EmitCommandLog(string ipatoolPath, IReadOnlyList<string> arguments)
        {
            if (!KeychainConfig.GetDetailedIpatoolLogEnabled())
            {
                return;
            }

            string rendered = RenderArgumentsForDisplay(arguments);
            CommandExecuting?.Invoke($"ipatool {rendered}");
        }

        private static void EmitDetailedOutputLogs(string? output, string? error)
        {
            if (!KeychainConfig.GetDetailedIpatoolLogEnabled())
            {
                return;
            }

            foreach (string line in EnumerateLines(output))
            {
                CommandOutputReceived?.Invoke(SanitizeLogText(line));
            }

            foreach (string line in EnumerateLines(error))
            {
                CommandOutputReceived?.Invoke(SanitizeLogText(line));
            }
        }

        private static string SanitizeLogText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return SensitiveJsonPropertyRegex.Replace(value, "$1\"***\"");
        }

        private static IEnumerable<string> EnumerateLines(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    yield return line;
                }
            }
        }

        private static string FormatArgForDisplay(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return "\"\"";
            }

            if (!argument.Any(char.IsWhiteSpace) && !argument.Contains('"'))
            {
                return argument;
            }

            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }

        private static string RenderArgumentsForDisplay(IReadOnlyList<string> arguments)
        {
            var rendered = new List<string>(arguments.Count);
            for (int i = 0; i < arguments.Count; i++)
            {
                string arg = arguments[i];
                rendered.Add(FormatArgForDisplay(arg));

                if (!SensitiveSwitches.Contains(arg))
                {
                    continue;
                }

                int nextIndex = i + 1;
                if (nextIndex >= arguments.Count)
                {
                    continue;
                }

                rendered.Add("\"***\"");
                i++;
            }

            return string.Join(" ", rendered);
        }

        private static string L(string key)
        {
            return Loader.GetString(key);
        }

        private static string LF(string key, params object[] args)
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, L(key), args);
        }
    }
}
