using IPAbuyer.Core.Models;
using IPAbuyer.Core.Services.Purchases;
using System.Globalization;
using System.Text.Json;

namespace IPAbuyer.Core.Services.AppCatalog
{
    internal static class AppStoreSearchResponseParser
    {
        internal static IReadOnlyList<SearchResult>? Parse(string payload, IReadOnlyDictionary<string, string> purchasedApps)
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("results", out JsonElement results)
                || results.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var items = new List<SearchResult>();
            foreach (JsonElement app in results.EnumerateArray())
            {
                string bundleId = GetBundleId(app) ?? string.Empty;
                string price = PurchaseStatusPolicy.NormalizePriceForDisplay(GetPriceValue(app));
                items.Add(new SearchResult
                {
                    bundleId = bundleId,
                    id = GetPropertyValue(app, "trackId"),
                    name = GetPropertyValue(app, "trackName"),
                    developer = GetPropertyValue(app, "sellerName"),
                    artworkUrl = GetPropertyValue(app, "artworkUrl100"),
                    price = price,
                    version = GetPropertyValue(app, "version"),
                    purchased = PurchaseStatusPolicy.ResolveSearchStatus(bundleId, price, purchasedApps)
                });
            }

            return items;
        }

        private static string? GetBundleId(JsonElement element)
        {
            return GetPropertyValue(element, "bundleID") ?? GetPropertyValue(element, "bundleId");
        }

        private static string? GetPropertyValue(JsonElement element, string name)
        {
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement property)
                ? ReadScalar(property)
                : null;
        }

        private static string? GetPriceValue(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("price", out JsonElement price))
            {
                return null;
            }

            return price.ValueKind == JsonValueKind.Number && price.TryGetDecimal(out decimal value)
                ? value.ToString("0.00", CultureInfo.InvariantCulture)
                : ReadScalar(price);
        }

        private static string? ReadScalar(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetDecimal(out decimal value) => value.ToString(CultureInfo.InvariantCulture),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => element.GetRawText()
            };
        }
    }
}
