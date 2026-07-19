using Microsoft.UI.Xaml;

namespace IPAbuyer.Pages
{
    internal static class WindowContext
    {
        public static Window? MainWindow { get; private set; }

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
