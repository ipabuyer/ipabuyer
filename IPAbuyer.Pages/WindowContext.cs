using Microsoft.UI.Xaml;

namespace IPAbuyer.Pages
{
    public static class WindowContext
    {
        private static Func<string?>? _restartApplication;

        public static Window? MainWindow { get; private set; }

        public static void RegisterRestartHandler(Func<string?> restartApplication)
        {
            _restartApplication = restartApplication;
        }

        public static string? RequestRestart()
        {
            Func<string?>? restartApplication = _restartApplication;
            if (restartApplication == null)
            {
                return "Restart handler is unavailable.";
            }

            return restartApplication();
        }

        public static void SetMainWindow(Window window)
        {
            MainWindow = window;
        }

        public static void ClearMainWindow(Window window)
        {
            if (ReferenceEquals(MainWindow, window))
            {
                MainWindow = null;
            }
        }
    }
}
