using IPAbuyer.Core.Configuration;
using IPAbuyer.Core.Integration.Ipatool;
using IPAbuyer.Core.Logging;
using IPAbuyer.Core.Services.Authentication;
using IPAbuyer.Core.Services.Downloads;
using IPAbuyer.Core.State;
using IPAbuyer.Core.Data.PurchasedApps;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Globalization;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace IPAbuyer.Pages
{
    public sealed partial class Settings : Page
    {
        private static readonly ResourceLoader Loader = new();
        private sealed record StorefrontPickerItem(AppleStorefront Storefront, string DisplayName)
        {
            public string Code => Storefront.Code;

            public string DisplayText => $"{DisplayName} ({Code.ToUpperInvariant()})";

            public string SearchText => $"{Storefront.SearchText} {DisplayName}";
        }

        private bool _isInitializingLanguageOption;
        private bool _isInitializingOwnedCheckOption;
        private bool _isInitializingPassphraseRotationOption;

        public Settings()
        {
            _isInitializingLanguageOption = true;
            InitializeComponent();
            InitializeDisplayLanguage();
            InitializeCountryCode();
            InitializeDownloadDirectory();
            InitializeOwnedCheckOption();
            InitializeKeychainPassphraseRotationOption();
            InitializeAppVersion();
        }

        private void GithubButton(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ipa.blazesnow.com/",
                UseShellExecute = true
            });
        }

        private async void DeleteDataBase(object sender, RoutedEventArgs e)
        {
            int totalBefore = PurchasedAppDb.GetTotalCount();
            var dialog = new ContentDialog
            {
                Title = L("Settings/Dialog/ConfirmAction/Title"),
                Content = LF(
                    "Settings/Database/Clear/ConfirmMessage",
                    Environment.NewLine,
                    totalBefore),
                PrimaryButtonText = L("Settings/Dialog/ConfirmAction/Primary"),
                CloseButtonText = L("Settings/Dialog/ConfirmAction/Close"),
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                PurchasedAppDb.ClearPurchasedApps();
                int totalAfter = PurchasedAppDb.GetTotalCount();
                await ShowDialogAsync(
                    L("Settings/Dialog/SuccessTitle"),
                    LF(
                        "Settings/Database/Clear/SuccessMessage",
                        Environment.NewLine,
                        totalBefore,
                        totalAfter));
            }
            catch (Exception ex)
            {
                await ShowDialogAsync(
                    L("Settings/Dialog/ErrorTitle"),
                    LF("Settings/Database/Clear/FailMessage", ex.Message));
            }
        }

        private void InitializeDisplayLanguage()
        {
            _isInitializingLanguageOption = true;
            try
            {
                string preference = LanguageSettings.GetPreference();
                DisplayLanguageComboBox.SelectedIndex = preference switch
                {
                    LanguageSettings.ChineseLanguage => 1,
                    LanguageSettings.EnglishLanguage => 2,
                    _ => 0
                };
            }
            finally
            {
                _isInitializingLanguageOption = false;
            }
        }

        private async void DisplayLanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingLanguageOption
                || XamlRoot == null
                || DisplayLanguageComboBox.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            string previousPreference = LanguageSettings.GetPreference();
            string selectedPreference = LanguageSettings.Normalize(item.Tag as string);
            if (string.Equals(previousPreference, selectedPreference, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var dialog = new ContentDialog
                {
                    Title = L("Settings/Language/RestartTitle"),
                    Content = L("Settings/Language/RestartMessage"),
                    PrimaryButtonText = L("Settings/Language/RestartNow"),
                    CloseButtonText = L("Settings/Language/RestartLater"),
                    XamlRoot = XamlRoot
                };

                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    SelectDisplayLanguage(previousPreference);
                    return;
                }

                LanguageSettings.SavePreference(selectedPreference);
                var failureReason = Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
                await ShowDialogAsync(
                    L("Settings/Language/RestartFailedTitle"),
                    LF("Settings/Language/RestartFailedMessage", failureReason));
            }
            catch (Exception ex)
            {
                LanguageSettings.SavePreference(previousPreference);
                SelectDisplayLanguage(previousPreference);

                if (XamlRoot != null)
                {
                    await ShowDialogAsync(
                        L("Settings/Language/RestartFailedTitle"),
                        LF("Settings/Language/RestartFailedMessage", ex.Message));
                }
            }
        }

        private void SelectDisplayLanguage(string preference)
        {
            _isInitializingLanguageOption = true;
            try
            {
                DisplayLanguageComboBox.SelectedIndex = LanguageSettings.Normalize(preference) switch
                {
                    LanguageSettings.ChineseLanguage => 1,
                    LanguageSettings.EnglishLanguage => 2,
                    _ => 0
                };
            }
            finally
            {
                _isInitializingLanguageOption = false;
            }
        }

        private void InitializeCountryCode()
        {
            try
            {
                string currentCode = KeychainConfig.GetCountryCode();
                if (CountryCodeValueTextBlockControl != null)
                {
                    CountryCodeValueTextBlockControl.Text = LF("Settings/CountryCode/CurrentFormat", currentCode);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(LF("Settings/Debug/InitializeCountryCodeFailed", ex.Message));
            }
        }

        private void InitializeDownloadDirectory()
        {
            try
            {
                if (DownloadDirectoryValueTextBlockControl != null)
                {
                    DownloadDirectoryValueTextBlockControl.Text = KeychainConfig.GetDownloadDirectory();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(LF("Settings/Debug/InitializeDownloadDirectoryFailed", ex.Message));
            }
        }

        private async void CountryCodeButton(object sender, RoutedEventArgs e)
        {
            await HandleCountryCodeSubmissionAsync();
        }

        private static IReadOnlyList<StorefrontPickerItem> CreateStorefrontPickerItems()
        {
            return AppleStorefrontCatalog.All
                .Select(storefront => new StorefrontPickerItem(
                    storefront,
                    L($"Storefront/{storefront.Code.ToUpperInvariant()}")))
                .OrderBy(storefront => storefront.DisplayName, StringComparer.CurrentCulture)
                .ToArray();
        }

        private async Task HandleCountryCodeSubmissionAsync()
        {
            string currentCode = KeychainConfig.GetCountryCode();
            IReadOnlyList<StorefrontPickerItem> allStorefronts = CreateStorefrontPickerItems();
            var filteredStorefronts = new ObservableCollection<StorefrontPickerItem>(allStorefronts);
            var searchBox = new TextBox
            {
                PlaceholderText = L("Settings/CountryCode/SearchPlaceholder")
            };
            var storefrontList = new ListView
            {
                ItemsSource = filteredStorefronts,
                SelectionMode = ListViewSelectionMode.Single,
                DisplayMemberPath = nameof(StorefrontPickerItem.DisplayText),
                MaxHeight = 440,
                MinWidth = 360
            };
            storefrontList.SelectedItem = filteredStorefronts.FirstOrDefault(storefront =>
                string.Equals(storefront.Code, currentCode, StringComparison.OrdinalIgnoreCase));

            var noResultsTextBlock = new TextBlock
            {
                Text = L("Settings/CountryCode/NoResults"),
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x60, 0x60, 0x60)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = filteredStorefronts.Count == 0 ? Visibility.Visible : Visibility.Collapsed
            };

            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(searchBox);
            content.Children.Add(noResultsTextBlock);
            content.Children.Add(storefrontList);

            var dialog = new ContentDialog
            {
                Title = L("Settings/CountryCode/DialogTitle"),
                Content = content,
                PrimaryButtonText = L("Settings/CountryCode/SaveButton"),
                CloseButtonText = L("Settings/CountryCode/CancelButton"),
                IsPrimaryButtonEnabled = storefrontList.SelectedItem is StorefrontPickerItem,
                XamlRoot = XamlRoot
            };

            void FilterStorefronts(string query)
            {
                string normalizedQuery = query.Trim();
                StorefrontPickerItem? selectedStorefront = storefrontList.SelectedItem as StorefrontPickerItem;
                filteredStorefronts.Clear();
                foreach (StorefrontPickerItem storefront in allStorefronts)
                {
                    if (string.IsNullOrWhiteSpace(normalizedQuery)
                        || storefront.SearchText.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                    {
                        filteredStorefronts.Add(storefront);
                    }
                }

                storefrontList.SelectedItem = selectedStorefront != null && filteredStorefronts.Contains(selectedStorefront)
                    ? selectedStorefront
                    : null;
                noResultsTextBlock.Visibility = filteredStorefronts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                dialog.IsPrimaryButtonEnabled = storefrontList.SelectedItem is StorefrontPickerItem;
            }

            searchBox.TextChanged += (_, _) => FilterStorefronts(searchBox.Text ?? string.Empty);
            storefrontList.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = storefrontList.SelectedItem is StorefrontPickerItem;

            if (storefrontList.SelectedItem != null)
            {
                storefrontList.ScrollIntoView(storefrontList.SelectedItem);
            }

            if (await dialog.ShowAsync() != ContentDialogResult.Primary
                || storefrontList.SelectedItem is not StorefrontPickerItem selectedStorefront)
            {
                return;
            }

            try
            {
                KeychainConfig.SaveCountryCode(selectedStorefront.Code);
                if (CountryCodeValueTextBlockControl != null)
                {
                    CountryCodeValueTextBlockControl.Text = LF("Settings/CountryCode/CurrentFormat", selectedStorefront.Code);
                }

                MainPageCacheState.InvalidateSearchCache();
                await ShowDialogAsync(
                    L("Settings/Dialog/SuccessTitle"),
                    LF("Settings/CountryCode/UpdatedMessage", selectedStorefront.Code));
            }
            catch (Exception ex)
            {
                await ShowDialogAsync(
                    L("Settings/Dialog/OperationFailedTitle"),
                    LF("Settings/CountryCode/SaveFailMessage", ex.Message));
            }
        }

        private async void ResetCountryCodeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                KeychainConfig.SaveCountryCode("cn");
                if (CountryCodeValueTextBlockControl != null)
                {
                    CountryCodeValueTextBlockControl.Text = LF("Settings/CountryCode/CurrentFormat", "cn");
                }

                MainPageCacheState.InvalidateSearchCache();
                await ShowDialogAsync(
                    L("Settings/Dialog/SuccessTitle"),
                    L("Settings/CountryCode/ResetSuccessMessage"));
            }
            catch (Exception ex)
            {
                await ShowDialogAsync(
                    L("Settings/Dialog/OperationFailedTitle"),
                    LF("Settings/CountryCode/ResetFailMessage", ex.Message));
            }
        }

        private async void PickDownloadDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.Downloads
            };
            folderPicker.FileTypeFilter.Add("*");

            try
            {
                if (WindowContext.MainWindow != null)
                {
                    IntPtr hwnd = WindowNative.GetWindowHandle(WindowContext.MainWindow);
                    InitializeWithWindow.Initialize(folderPicker, hwnd);
                }

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder == null)
                {
                    return;
                }

                KeychainConfig.SaveDownloadDirectory(folder.Path);
                if (DownloadDirectoryValueTextBlockControl != null)
                {
                    DownloadDirectoryValueTextBlockControl.Text = folder.Path;
                }

                await ShowDialogAsync(
                    L("Settings/Dialog/SuccessTitle"),
                    L("Settings/DownloadDirectory/UpdatedMessage"));
            }
            catch (Exception ex)
            {
                await ShowDialogAsync(
                    L("Settings/Dialog/OperationFailedTitle"),
                    LF("Settings/DownloadDirectory/SaveFailMessage", ex.Message));
            }
        }

        private async void ResetDownloadDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string defaultDirectory = KeychainConfig.GetDefaultDownloadDirectory();
                KeychainConfig.SaveDownloadDirectory(defaultDirectory);
                if (DownloadDirectoryValueTextBlockControl != null)
                {
                    DownloadDirectoryValueTextBlockControl.Text = defaultDirectory;
                }

                await ShowDialogAsync(
                    L("Settings/Dialog/SuccessTitle"),
                    LF("Settings/DownloadDirectory/ResetSuccessMessage", defaultDirectory));
            }
            catch (Exception ex)
            {
                await ShowDialogAsync(
                    L("Settings/Dialog/OperationFailedTitle"),
                    LF("Settings/DownloadDirectory/ResetFailMessage", ex.Message));
            }
        }

        private async void CopyFeedbackEmailButton_Click(object sender, RoutedEventArgs e)
        {
            const string feedbackEmail = "ipa@blazesnow.com";
            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(feedbackEmail);
                Clipboard.SetContent(dataPackage);
                Clipboard.Flush();
                await ShowDialogAsync(
                    L("Settings/Dialog/SuccessTitle"),
                    LF("Settings/Feedback/CopiedMessage", feedbackEmail));
            }
            catch (Exception ex)
            {
                await ShowDialogAsync(
                    L("Settings/Dialog/OperationFailedTitle"),
                    LF("Settings/Feedback/CopyFailMessage", ex.Message));
            }
        }

        private void InitializeAppVersion()
        {
            if (AppVersionValueTextBlock == null)
            {
                return;
            }

            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            AppVersionValueTextBlock.Text = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }

        private void InitializeOwnedCheckOption()
        {
            if (OwnedCheckBox == null)
            {
                return;
            }

            _isInitializingOwnedCheckOption = true;
            try
            {
                OwnedCheckBox.IsOn = KeychainConfig.GetOwnedCheckEnabled();
            }
            finally
            {
                _isInitializingOwnedCheckOption = false;
            }
        }

        private void OwnedCheckBox_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializingOwnedCheckOption)
            {
                return;
            }

            KeychainConfig.SaveOwnedCheckEnabled(OwnedCheckBox.IsOn);
        }

        private void InitializeKeychainPassphraseRotationOption()
        {
            if (KeychainPassphraseRotationCheckBox == null)
            {
                return;
            }

            _isInitializingPassphraseRotationOption = true;
            try
            {
                KeychainPassphraseRotationCheckBox.IsOn = KeychainConfig.GetKeychainPassphraseRotationEnabled();
            }
            finally
            {
                _isInitializingPassphraseRotationOption = false;
            }
        }

        private void KeychainPassphraseRotationCheckBox_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializingPassphraseRotationOption)
            {
                return;
            }

            KeychainConfig.SaveKeychainPassphraseRotationEnabled(KeychainPassphraseRotationCheckBox.IsOn);
        }

        private async Task ShowDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = L("Settings/Dialog/CloseButton"),
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }

        private TextBlock? CountryCodeValueTextBlockControl => FindName("CountryCodeValueTextBlock") as TextBlock;
        private TextBlock? DownloadDirectoryValueTextBlockControl => FindName("DownloadDirectoryValueTextBlock") as TextBlock;

        private static string L(string key)
        {
            return Loader.GetString(key);
        }

        private static string LF(string key, params object[] args)
        {
            string format = L(key);
            return string.Format(format, args);
        }
    }
}
