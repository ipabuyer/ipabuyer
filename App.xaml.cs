using IPAbuyer.Core.Configuration;
using IPAbuyer.Core.Integration.Ipatool;
using IPAbuyer.Core.Services.Purchases;
using IPAbuyer.Core.State;
using IPAbuyer.Pages;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;
using System.Diagnostics;
using Windows.Globalization;
using Windows.Storage;

namespace IPAbuyer
{
    public partial class App : Application
    {
        private static readonly ResourceLoader Loader = new();
        private Window? _window;
        public Window? MainWindowInstance => _window;

        // 构造函数
        public App()
        {
            try
            {
                try
                {
                    ApplicationLanguages.PrimaryLanguageOverride = LanguageSettings.LoadResolvedLanguage();
                }
                catch
                {
                    // Language preference must never prevent the app from starting.
                }

                WindowContext.RegisterRestartHandler(RestartApplication);

                try
                {
                    // 初始化数据库
                    PurchaseHistoryService.Initialize();
                }
                catch (Exception ex)
                {
                    WriteStartupLog($"[InitDb] {ex.GetType().FullName}: {ex.Message}");
                }

                try
                {
                    // 初始化 LocalSettings、凭据和 legacy 配置迁移。
                    ConfigurationBootstrapper.Initialize();
                }
                catch (Exception ex)
                {
                    WriteStartupLog($"[InitConfig] {ex.GetType().FullName}: {ex.Message}");
                }

                InitializeComponent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(LF("App/Debug/StartupError", ex.Message));
                throw;
            }
        }

        // 应用启动时调用
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                _ = WarmupAuthInfoAsync();
                _window = new MainWindow();
                // 激活窗口
                _window.Activate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(LF("App/Debug/StartupError", ex.Message));
                throw;
            }
        }

        private static string? RestartApplication()
        {
            try
            {
                var reason = Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
                // None 表示重启已发起，返回 null 让调用方不提示失败。
                return reason == Microsoft.Windows.AppLifecycle.AppRestartFailureReason.None ? null : reason.ToString();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static async Task WarmupAuthInfoAsync()
        {
            try
            {
                var result = await IpatoolClient.AuthInfoAsync(
                    passphrase: null,
                    silent: true).ConfigureAwait(false);
                string account = IpatoolClient.ExtractEmailFromPayload(result.OutputOrError);
                bool isAuthSuccess = result.IsSuccessResponse
                    && !IpatoolClient.HasExplicitFailureFlag(result.OutputOrError)
                    && (IpatoolClient.IsPayloadSuccess(result.OutputOrError) || !string.IsNullOrWhiteSpace(account));
                if (!isAuthSuccess)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(account))
                {
                    SessionState.Reset();
                    return;
                }

                SessionState.SetLoginState(account, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(LF("App/Debug/WarmupAuthInfoFailed", ex.Message));
            }
        }

        private static string LF(string key, params object[] args)
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, Loader.GetString(key), args);
        }

        private static void WriteStartupLog(string message)
        {
            try
            {
                string dir;
                try
                {
                    dir = ApplicationData.Current.LocalFolder.Path;
                    if (string.IsNullOrWhiteSpace(dir))
                    {
                        throw new InvalidOperationException("LocalFolder path is empty.");
                    }
                }
                catch
                {
                    dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IPAbuyer");
                }

                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "crash.log");
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // ignore startup logging failures
            }
        }
    }
}
