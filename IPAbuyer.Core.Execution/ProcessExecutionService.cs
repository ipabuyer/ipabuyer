using System.Diagnostics;
using System.Text;

namespace IPAbuyer.Core.Execution
{
    public sealed class ProcessExecutionService
    {
        private readonly object _activeProcessesLock = new();
        private readonly HashSet<Process> _activeProcesses = new();
        private readonly CancellationTokenSource _shutdownCts = new();
        private bool _isShuttingDown;

        public void BeginShutdown()
        {
            Process[] processes;
            lock (_activeProcessesLock)
            {
                if (_isShuttingDown)
                {
                    return;
                }

                _isShuttingDown = true;
                _shutdownCts.Cancel();
                processes = _activeProcesses.ToArray();
            }

            foreach (Process process in processes)
            {
                TryTerminateProcess(process);
            }
        }

        public async Task<ProcessExecutionResult> ExecuteAsync(
            ProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            Process? process = null;
            Task? stdoutTask = null;
            Task? stderrTask = null;

            try
            {
                process = new Process
                {
                    StartInfo = CreateProcessStartInfo(request),
                    EnableRaisingEvents = true
                };
                if (!process.Start())
                {
                    throw new InvalidOperationException("Failed to start the process.");
                }

                if (!RegisterProcess(process))
                {
                    await WaitForProcessCleanupAsync(process, null, null).ConfigureAwait(false);
                    throw new OperationCanceledException(_shutdownCts.Token);
                }

                // 立即关闭标准输入：子进程永远读不到输入，任何交互式提示都会因 EOF 立即结束而不是挂起等待。
                try
                {
                    process.StandardInput.Close();
                }
                catch
                {
                    // 进程可能在关闭输入前已经退出。
                }

                using var timeoutCts = new CancellationTokenSource(request.Timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _shutdownCts.Token,
                    timeoutCts.Token);

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                stdoutTask = ReadProcessStreamAsync(process.StandardOutput, outputBuilder, request.OutputChunkReceived, linkedCts.Token);
                stderrTask = ReadProcessStreamAsync(process.StandardError, errorBuilder, request.OutputChunkReceived, linkedCts.Token);

                try
                {
                    await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
                    await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    TryTerminateProcess(process);
                    await WaitForProcessCleanupAsync(process, stdoutTask, stderrTask).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    _shutdownCts.Token.ThrowIfCancellationRequested();

                    return new ProcessExecutionResult(string.Empty, string.Empty, -1, TimedOut: true);
                }

                return new ProcessExecutionResult(
                    outputBuilder.ToString(),
                    errorBuilder.ToString(),
                    process.ExitCode,
                    TimedOut: false);
            }
            finally
            {
                if (process != null)
                {
                    UnregisterProcess(process);
                    process.Dispose();
                }
            }
        }

        private static ProcessStartInfo CreateProcessStartInfo(ProcessExecutionRequest request)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = request.FileName,
                WorkingDirectory = request.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            foreach (string argument in request.Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            if (request.EnvironmentVariables != null)
            {
                foreach ((string key, string value) in request.EnvironmentVariables)
                {
                    startInfo.Environment[key] = value;
                }
            }

            return startInfo;
        }

        private static async Task ReadProcessStreamAsync(
            StreamReader reader,
            StringBuilder builder,
            Action<string>? outputChunkCallback,
            CancellationToken cancellationToken)
        {
            char[] buffer = new char[1024];
            while (!cancellationToken.IsCancellationRequested)
            {
                int read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                string chunk = new(buffer, 0, read);
                builder.Append(chunk);
                outputChunkCallback?.Invoke(chunk);
            }
        }

        private bool RegisterProcess(Process process)
        {
            lock (_activeProcessesLock)
            {
                if (_isShuttingDown)
                {
                    TryTerminateProcess(process);
                    return false;
                }

                _activeProcesses.Add(process);
                return true;
            }
        }

        private void UnregisterProcess(Process process)
        {
            lock (_activeProcessesLock)
            {
                _activeProcesses.Remove(process);
            }
        }

        private static async Task WaitForProcessCleanupAsync(Process process, Task? stdoutTask, Task? stderrTask)
        {
            try
            {
                if (!process.HasExited)
                {
                    await process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                // Process termination can race with exit and handle cleanup.
            }

            if (stdoutTask != null || stderrTask != null)
            {
                try
                {
                    await Task.WhenAll(stdoutTask ?? Task.CompletedTask, stderrTask ?? Task.CompletedTask).ConfigureAwait(false);
                }
                catch
                {
                    // Process termination can concurrently cancel or fault both redirected-pipe readers.
                }
            }
        }

        private static void TryTerminateProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore termination races with natural process exit.
            }
        }
    }
}
