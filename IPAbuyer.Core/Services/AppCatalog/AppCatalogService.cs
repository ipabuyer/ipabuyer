using IPAbuyer.Core.Configuration;
using IPAbuyer.Core.Models;
using IPAbuyer.Core.Services.Purchases;

namespace IPAbuyer.Core.Services.AppCatalog
{
    public static class AppCatalogService
    {
        public static async Task<IReadOnlyList<SearchResult>?> SearchAsync(string appName, int limit, string account, CancellationToken cancellationToken)
        {
            string countryCode = NormalizeCountryCode(ApplicationSettings.GetCountryCode());
            var response = await AppleAppStoreSearchClient.SearchAsync(appName, limit, countryCode, cancellationToken).ConfigureAwait(false);
            if (response.TimedOut || string.IsNullOrWhiteSpace(response.OutputOrError))
            {
                return null;
            }

            var purchasedApps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(account))
            {
                foreach ((string appId, string status) in PurchaseHistoryService.GetForAccount(account))
                {
                    if (!string.IsNullOrWhiteSpace(appId))
                    {
                        purchasedApps[appId] = status;
                    }
                }
            }

            return AppStoreSearchResponseParser.Parse(response.OutputOrError, purchasedApps);
        }

        public static string NormalizeCountryCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return "cn";
            }

            string normalized = code.Trim().ToLowerInvariant();
            return ApplicationSettings.IsValidCountryCode(normalized) ? normalized : "cn";
        }
    }
}
