namespace IPAbuyer.Core.Execution
{
    public sealed record ProcessExecutionResult(
        string StandardOutput,
        string StandardError,
        int ExitCode,
        bool TimedOut);
}
