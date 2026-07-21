namespace IPAbuyer.Core.Execution
{
    public sealed record ProcessExecutionRequest(
        string FileName,
        string WorkingDirectory,
        IReadOnlyList<string> Arguments,
        TimeSpan Timeout,
        IReadOnlyDictionary<string, string>? EnvironmentVariables = null,
        Action<string>? OutputChunkReceived = null);
}
