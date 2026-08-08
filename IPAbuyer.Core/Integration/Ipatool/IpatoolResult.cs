namespace IPAbuyer.Core.Integration.Ipatool
{
    public sealed record IpatoolResult(string? Output, string? Error, int ExitCode, bool TimedOut)
    {
        public string OutputOrError => string.IsNullOrWhiteSpace(Output) ? Error ?? string.Empty : Output;
        public bool IsSuccessResponse => !TimedOut && ExitCode == 0;
    }
}
