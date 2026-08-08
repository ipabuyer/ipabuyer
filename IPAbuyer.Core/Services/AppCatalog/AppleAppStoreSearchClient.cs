using IPAbuyer.Core.Integration.Ipatool;
using Microsoft.Windows.ApplicationModel.Resources;

namespace IPAbuyer.Core.Services.AppCatalog
{
    public static class AppleAppStoreSearchClient
    {
        private static readonly ResourceLoader Loader = new();
        private static readonly HttpClient HttpClient = new();
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

        public static async Task<IpatoolResult> SearchAsync(string name, int limit, string countryCode, CancellationToken cancellationToken)
        {
            string query = name?.Trim() ?? string.Empty;
            if (query.Length == 0)
            {
                throw new ArgumentException(Loader.GetString("Ipatool/Error/AppNameRequired"), nameof(name));
            }

            string country = string.IsNullOrWhiteSpace(countryCode) ? "cn" : countryCode.Trim().ToLowerInvariant();
            string uri = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&entity=software&limit={Math.Max(1, limit)}&country={Uri.EscapeDataString(country)}";
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(Timeout);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.TryAddWithoutValidation("User-Agent", "IPAbuyer/1.0");
                using HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutCts.Token).ConfigureAwait(false);
                string content = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    string message = string.Format(System.Globalization.CultureInfo.CurrentCulture, Loader.GetString("Ipatool/Error/HttpRequestFailed"), (int)response.StatusCode, response.ReasonPhrase ?? string.Empty);
                    return new IpatoolResult(null, string.IsNullOrWhiteSpace(content) ? message : content, (int)response.StatusCode, false);
                }

                return new IpatoolResult(content, null, 0, false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new IpatoolResult(null, string.Format(System.Globalization.CultureInfo.CurrentCulture, Loader.GetString("Ipatool/Error/ExecutionTimeout"), uri), -1, true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new IpatoolResult(null, ex.Message, -1, false);
            }
        }
    }
}
