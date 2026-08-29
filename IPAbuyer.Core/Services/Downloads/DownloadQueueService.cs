using IPAbuyer.Core.Configuration;
using IPAbuyer.Core.Integration.Ipatool;
using IPAbuyer.Core.Logging;
using IPAbuyer.Core.Models;
using IPAbuyer.Core.State;
using Microsoft.Windows.ApplicationModel.Resources;
using System.Collections.ObjectModel;
using System.Globalization;

namespace IPAbuyer.Core.Services.Downloads
{
    public enum DownloadQueueAddResult
    {
        Ignored = 0,
        Added = 1,
        Updated = 2,
        Requeued = 3
    }

    public sealed class DownloadQueueService
    {
        private static readonly ResourceLoader Loader = new();

        private readonly ObservableCollection<DownloadQueueItem> _items = new();
        private readonly SemaphoreSlim _runLock = new(1, 1);
        private readonly object _cancellationLock = new();
        private CancellationTokenSource? _queueCts;
        private CancellationTokenSource? _currentItemCts;
        private bool _isRunning;

        private DownloadQueueService()
        {
        }

        public static DownloadQueueService Instance { get; } = new();

        public ObservableCollection<DownloadQueueItem> Items => _items;
        public bool IsRunning => _isRunning;

        public event Action<UiLogMessage>? LogReceived;
        public event Action? QueueChanged;

        public DownloadQueueAddResult AddOrUpdateFromSearchResult(SearchResult app)
        {
            if (app == null || string.IsNullOrWhiteSpace(app.bundleId))
            {
                return DownloadQueueAddResult.Ignored;
            }

            string bundleId = app.bundleId.Trim();
            var existing = _items.FirstOrDefault(i => i.BundleId == bundleId);
            if (existing != null)
            {
                existing.AppId = app.id ?? existing.AppId;
                existing.Name = app.name ?? existing.Name;
                existing.Developer = app.developer ?? existing.Developer;
                existing.Version = app.version ?? existing.Version;
                existing.Price = app.price ?? existing.Price;
                existing.ArtworkUrl = app.artworkUrl ?? existing.ArtworkUrl;

                bool requeued = existing.Status is DownloadQueueStatus.Failed or DownloadQueueStatus.Canceled or DownloadQueueStatus.Success;
                if (requeued)
                {
                    existing.Status = DownloadQueueStatus.Pending;
                    existing.LastMessage = L("DownloadQueue/Status/Requeued");
                }

                EmitLog(requeued
                    ? LF("DownloadQueue/Log/Requeued", existing.Name, existing.BundleId)
                    : LF("DownloadQueue/Log/Updated", existing.Name, existing.BundleId),
                    UiLogLevel.Success);
                NotifyQueueChanged();
                return requeued ? DownloadQueueAddResult.Requeued : DownloadQueueAddResult.Updated;
            }

            var item = new DownloadQueueItem
            {
                BundleId = bundleId,
                AppId = app.id ?? string.Empty,
                Name = app.name ?? bundleId,
                Developer = app.developer ?? string.Empty,
                Version = app.version ?? string.Empty,
                Price = app.price ?? string.Empty,
                ArtworkUrl = app.artworkUrl ?? string.Empty,
                Status = DownloadQueueStatus.Pending,
                LastMessage = L("DownloadQueue/Status/Pending")
            };

            _items.Add(item);
            EmitLog(LF("DownloadQueue/Log/Added", item.Name, item.BundleId), UiLogLevel.Success);
            NotifyQueueChanged();
            return DownloadQueueAddResult.Added;
        }

        public int RemoveItems(System.Collections.Generic.IEnumerable<DownloadQueueItem> items)
        {
            if (items == null)
            {
                return 0;
            }

            var removing = items.ToList();
            int removed = 0;

            foreach (var item in removing)
            {
                if (_isRunning && item.Status == DownloadQueueStatus.Downloading)
                {
                    continue;
                }

                if (_items.Remove(item))
                {
                    removed++;
                }
            }

            if (removed > 0)
            {
                EmitLog(LF("DownloadQueue/Log/Removed", removed), UiLogLevel.Success);
                NotifyQueueChanged();
            }

            return removed;
        }

        public async Task<int> StartQueueAsync()
        {
            await _runLock.WaitAsync();
            CancellationTokenSource? queueCts = null;
            try
            {
                if (_isRunning)
                {
                    EmitLog(L("DownloadQueue/Log/AlreadyRunning"), UiLogLevel.Info);
                    return 0;
                }

                int initialCount = CountRunnableItems();
                if (initialCount == 0)
                {
                    EmitLog(L("DownloadQueue/Log/NoPendingItems"), UiLogLevel.Tip);
                    return 0;
                }

                string account;
                try
                {
                    account = ResolveAccount();
                }
                catch (InvalidOperationException ex)
                {
                    EmitLog(ex.Message, UiLogLevel.Error);
                    return 0;
                }

                _isRunning = true;
                queueCts = new CancellationTokenSource();
                lock (_cancellationLock)
                {
                    _queueCts = queueCts;
                }
                NotifyQueueChanged();

                string outputDirectory = ApplicationSettings.GetDownloadDirectory();
                Directory.CreateDirectory(outputDirectory);
                bool useMockFlow = SessionState.IsLoggedIn
                    && SessionState.IsMockAccount
                    && string.Equals(SessionState.CurrentAccount, account, StringComparison.OrdinalIgnoreCase);

                EmitLog(LF("DownloadQueue/Log/StartQueue", initialCount, outputDirectory), UiLogLevel.Info);

                // 详细日志设置在队列运行期间缓存，避免下载输出高频回调反复读写配置。
                bool detailedLogEnabled = ApplicationSettings.GetDetailedIpatoolLogEnabled();
                int completed = 0;
                int processed = 0;
                var processedItems = new System.Collections.Generic.HashSet<DownloadQueueItem>();
                while (TryGetNextRunnableItem(processedItems, out var item))
                {
                    queueCts!.Token.ThrowIfCancellationRequested();
                    processed++;
                    processedItems.Add(item);

                    item.Status = DownloadQueueStatus.Downloading;
                    UpdateDownloadStage(item, "DownloadQueue/Status/Downloading", "DownloadQueue/Log/Downloading");

                    var itemCts = CancellationTokenSource.CreateLinkedTokenSource(queueCts!.Token);
                    lock (_cancellationLock)
                    {
                        _currentItemCts = itemCts;
                    }
                    try
                    {
                        if (useMockFlow)
                        {
                            item.Status = DownloadQueueStatus.Success;
                            item.LastMessage = L("DownloadQueue/Status/Success");
                            completed++;
                            EmitLog(LF("DownloadQueue/Log/MockSuccess", item.Name), UiLogLevel.Success);
                        }
                        else
                        {
                            var outputParser = new DownloadOutputParser();
                            object chunkLogSync = new();
                            var result = await IpatoolClient.DownloadAppWithProgressAsync(
                                item.BundleId,
                                outputDirectory,
                                account,
                                chunk =>
                                {
                                    if (itemCts.IsCancellationRequested)
                                    {
                                        return;
                                    }

                                    lock (chunkLogSync)
                                    {
                                        if (!itemCts.IsCancellationRequested)
                                        {
                                            ApplyOutputUpdate(item, outputParser.ProcessChunk(chunk), detailedLogEnabled);
                                        }
                                    }
                                },
                                itemCts.Token);

                            if (!itemCts.IsCancellationRequested)
                            {
                                lock (chunkLogSync)
                                {
                                    ApplyOutputUpdate(item, outputParser.Flush(), detailedLogEnabled);
                                }
                            }

                            itemCts.Token.ThrowIfCancellationRequested();
                            UpdateDownloadStage(item, "DownloadQueue/Status/ProcessingResult", "DownloadQueue/Log/ProcessingResult");

                            if (DownloadResultParser.IsSuccess(result))
                            {
                                item.Status = DownloadQueueStatus.Success;
                                item.LastMessage = L("DownloadQueue/Status/Success");
                                completed++;
                                EmitLog(LF("DownloadQueue/Log/Success", item.Name), UiLogLevel.Success);
                            }
                            else
                            {
                                string message = DownloadResultParser.GetErrorMessage(result);
                                item.Status = DownloadQueueStatus.Failed;
                                item.LastMessage = message;
                                EmitLog(LF("DownloadQueue/Log/Failed", item.Name, message), UiLogLevel.Error);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (itemCts.IsCancellationRequested)
                    {
                        item.Status = DownloadQueueStatus.Canceled;
                        item.LastMessage = L("DownloadQueue/Status/Canceled");
                        EmitLog(LF("DownloadQueue/Log/Canceled", item.Name), UiLogLevel.Tip);
                    }
                    catch (Exception ex)
                    {
                        item.Status = DownloadQueueStatus.Failed;
                        item.LastMessage = ex.Message;
                        EmitLog(LF("DownloadQueue/Log/Exception", item.Name, ex.Message), UiLogLevel.Error);
                    }
                    finally
                    {
                        lock (_cancellationLock)
                        {
                            if (ReferenceEquals(_currentItemCts, itemCts))
                            {
                                _currentItemCts = null;
                            }
                        }
                        itemCts.Dispose();
                        NotifyQueueChanged();
                    }
                }

                EmitLog(LF("DownloadQueue/Log/Completed", completed, processed), UiLogLevel.Info);
                return completed;
            }
            catch (OperationCanceledException) when (queueCts?.IsCancellationRequested == true)
            {
                EmitLog(L("DownloadQueue/Log/QueueCanceled"), UiLogLevel.Tip);
                return 0;
            }
            finally
            {
                _isRunning = false;
                lock (_cancellationLock)
                {
                    if (ReferenceEquals(_queueCts, queueCts))
                    {
                        _queueCts = null;
                    }
                    _currentItemCts = null;
                }
                queueCts?.Dispose();
                NotifyQueueChanged();
                _runLock.Release();
            }
        }

        public void CancelCurrent()
        {
            CancelCurrentItem();
            EmitLog(L("DownloadQueue/Log/CancelCurrentRequested"), UiLogLevel.Tip);
        }

        public void CancelAll()
        {
            CancelQueueAndCurrentItem();

            foreach (var item in _items.Where(i => i.Status == DownloadQueueStatus.Pending))
            {
                item.Status = DownloadQueueStatus.Canceled;
                item.LastMessage = L("DownloadQueue/Status/QueueCanceled");
            }

            EmitLog(L("DownloadQueue/Log/CancelAllRequested"), UiLogLevel.Tip);
            NotifyQueueChanged();
        }

        private void CancelCurrentItem()
        {
            lock (_cancellationLock)
            {
                CancelToken(_currentItemCts);
            }
        }

        private void CancelQueueAndCurrentItem()
        {
            lock (_cancellationLock)
            {
                CancelToken(_queueCts);
                CancelToken(_currentItemCts);
            }
        }

        private static void CancelToken(CancellationTokenSource? cancellation)
        {
            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // A completed queue may dispose its CTS while shutdown is being requested.
            }
        }

        private int CountRunnableItems()
        {
            return _items.Count(IsRunnableItem);
        }

        private bool TryGetNextRunnableItem(System.Collections.Generic.ISet<DownloadQueueItem> excludedItems, out DownloadQueueItem item)
        {
            DownloadQueueItem? nextItem = _items.FirstOrDefault(i => !excludedItems.Contains(i) && IsRunnableItem(i));
            if (nextItem == null)
            {
                item = null!;
                return false;
            }

            item = nextItem;
            return true;
        }

        private static bool IsRunnableItem(DownloadQueueItem item)
        {
            return item.Status == DownloadQueueStatus.Pending
                || item.Status == DownloadQueueStatus.Failed
                || item.Status == DownloadQueueStatus.Canceled;
        }

        private static string ResolveAccount()
        {
            string account = SessionState.IsLoggedIn ? SessionState.CurrentAccount : string.Empty;
            if (string.IsNullOrWhiteSpace(account))
            {
                if (SessionState.IsLoggedIn)
                {
                    throw new InvalidOperationException(L("DownloadQueue/Error/MissingSessionEmail"));
                }

                throw new InvalidOperationException(L("DownloadQueue/Error/MissingAccount"));
            }

            return account.Trim();
        }

        private void ApplyOutputUpdate(DownloadQueueItem item, DownloadOutputUpdate update, bool detailedLogEnabled)
        {
            if (update.RequestingLicense)
            {
                UpdateDownloadStage(item, "DownloadQueue/Status/RequestingLicense", "DownloadQueue/Log/RequestingLicense");
            }

            if (!detailedLogEnabled)
            {
                return;
            }

            foreach (string line in update.LogLines)
            {
                EmitLog($"[{item.Name}] {line}", UiLogLevel.Ipatool, UiLogSource.Ipatool);
            }
        }

        private void UpdateDownloadStage(DownloadQueueItem item, string statusKey, string logKey)
        {
            string status = L(statusKey);
            if (string.Equals(item.LastMessage, status, StringComparison.Ordinal))
            {
                return;
            }

            item.LastMessage = status;
            EmitLog(LF(logKey, item.Name), UiLogLevel.Info);
            NotifyQueueChanged();
        }

        private void EmitLog(string message, UiLogLevel level, UiLogSource source = UiLogSource.App)
        {
            LogReceived?.Invoke(new UiLogMessage(message, level, source));
        }

        private static string L(string key)
        {
            return Loader.GetString(key);
        }

        private static string LF(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, L(key), args);
        }

        private void NotifyQueueChanged()
        {
            QueueChanged?.Invoke();
        }
    }
}
