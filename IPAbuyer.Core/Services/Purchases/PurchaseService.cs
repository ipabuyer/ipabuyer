using IPAbuyer.Core.Integration.Ipatool;
using IPAbuyer.Core.Models;
using IPAbuyer.Core.State;

namespace IPAbuyer.Core.Services.Purchases
{
    public static class PurchaseService
    {
        public static async Task<PurchaseResult> PurchaseAsync(SearchResult app, string account, CancellationToken cancellationToken)
        {
            string bundleId = app.bundleId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(bundleId)
                || PurchaseStatusPolicy.IsPurchased(app.purchased)
                || PurchaseStatusPolicy.IsOwned(app.purchased))
            {
                return new PurchaseResult(bundleId, PurchaseOutcome.Skipped);
            }

            if (!PurchaseStatusPolicy.IsPriceFreeForPurchase(app.price))
            {
                return new PurchaseResult(bundleId, PurchaseOutcome.Skipped, "NonFree");
            }

            bool isMockAccount = SessionState.IsLoggedIn
                && SessionState.IsMockAccount
                && string.Equals(SessionState.CurrentAccount, account, StringComparison.OrdinalIgnoreCase);
            if (isMockAccount)
            {
                PurchaseHistoryService.Mark(bundleId, account, PurchaseStatusPolicy.PurchasedStatus);
                return new PurchaseResult(bundleId, PurchaseOutcome.Purchased, "Mock");
            }

            IpatoolResult response = await IpatoolClient.PurchaseAppAsync(bundleId, account, cancellationToken).ConfigureAwait(false);
            PurchaseOutcome outcome = PurchaseResponseInterpreter.Interpret(response.OutputOrError);
            if (outcome == PurchaseOutcome.Purchased)
            {
                PurchaseHistoryService.Mark(bundleId, account, PurchaseStatusPolicy.PurchasedStatus);
            }
            else if (outcome == PurchaseOutcome.AlreadyOwned)
            {
                PurchaseHistoryService.Mark(bundleId, account, PurchaseStatusPolicy.OwnedStatus);
            }

            return new PurchaseResult(bundleId, outcome, response.OutputOrError);
        }

        public static void Mark(string bundleId, string account, string status)
        {
            PurchaseHistoryService.Mark(bundleId, account, status);
        }

        public static void RemoveMark(string bundleId, string account)
        {
            PurchaseHistoryService.RemoveMark(bundleId, account);
        }
    }
}
