using IPAbuyer.Core.Serialization;

namespace IPAbuyer.Core.Services.Purchases
{
    public enum PurchaseOutcome
    {
        Skipped,
        Purchased,
        AlreadyOwned,
        NeedsOwnedConfirmation,
        Failed
    }

    public sealed record PurchaseResult(
        string BundleId,
        PurchaseOutcome Outcome,
        string? Detail = null);

    public static class PurchaseResponseInterpreter
    {
        public static PurchaseOutcome Interpret(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return PurchaseOutcome.Failed;
            }

            if (payload.Contains("\"alreadyOwned\":true", StringComparison.OrdinalIgnoreCase)
                || JsonPayload.EnumerateTokens(payload).Any(token => JsonPayload.TryReadBoolean(token, "alreadyOwned", out bool owned) && owned))
            {
                return PurchaseOutcome.AlreadyOwned;
            }

            if (payload.Contains("\"success\":true", StringComparison.OrdinalIgnoreCase)
                || JsonPayload.EnumerateTokens(payload).Any(token => JsonPayload.TryReadBoolean(token, "success", out bool success) && success))
            {
                return PurchaseOutcome.Purchased;
            }

            return payload.Contains("failed to purchase item with param 'STDQ'", StringComparison.OrdinalIgnoreCase)
                ? PurchaseOutcome.NeedsOwnedConfirmation
                : PurchaseOutcome.Failed;
        }
    }
}
