namespace IPAbuyer.Core.Services.Purchases
{
    internal static class PurchaseRecordStatus
    {
        internal const string Purchased = "purchased";
        internal const string Owned = "owned";

        internal static bool TryNormalize(string? status, out string normalizedStatus)
        {
            normalizedStatus = string.Empty;
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            string value = status.Trim();
            if (value.Equals(Purchased, StringComparison.OrdinalIgnoreCase)
                || value.Equals("已购买", StringComparison.Ordinal))
            {
                normalizedStatus = Purchased;
                return true;
            }

            if (value.Equals(Owned, StringComparison.OrdinalIgnoreCase)
                || value.Equals("Already owned", StringComparison.OrdinalIgnoreCase)
                || value.Equals("已拥有", StringComparison.Ordinal))
            {
                normalizedStatus = Owned;
                return true;
            }

            return false;
        }
    }
}
