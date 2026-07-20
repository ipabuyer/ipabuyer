using Windows.Storage;
using Windows.System.UserProfile;

namespace IPAbuyer.Core.Configuration
{
    public static class LanguageSettings
    {
        public const string AutoLanguage = "auto";
        public const string ChineseLanguage = "zh-Hans";
        public const string EnglishLanguage = "en-US";

        private const string LanguageSettingKey = "DisplayLanguage";

        public static string GetPreference()
        {
            try
            {
                var values = ApplicationData.Current.LocalSettings.Values;
                return Normalize(values.TryGetValue(LanguageSettingKey, out object? value) ? value as string : null);
            }
            catch
            {
                return AutoLanguage;
            }
        }

        public static void SavePreference(string language)
        {
            ApplicationData.Current.LocalSettings.Values[LanguageSettingKey] = Normalize(language);
        }

        public static string LoadResolvedLanguage()
        {
            try
            {
                return Resolve(GetPreference(), GlobalizationPreferences.Languages.FirstOrDefault());
            }
            catch
            {
                return EnglishLanguage;
            }
        }

        public static string Normalize(string? language)
        {
            if (string.Equals(language, ChineseLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return ChineseLanguage;
            }

            if (string.Equals(language, EnglishLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return EnglishLanguage;
            }

            return AutoLanguage;
        }

        public static string Resolve(string? preference, string? systemLanguage)
        {
            string normalizedPreference = Normalize(preference);
            if (normalizedPreference != AutoLanguage)
            {
                return normalizedPreference;
            }

            return IsSimplifiedChinese(systemLanguage) ? ChineseLanguage : EnglishLanguage;
        }

        private static bool IsSimplifiedChinese(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return false;
            }

            return language.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase)
                || language.StartsWith("zh-Hans-", StringComparison.OrdinalIgnoreCase)
                || language.Equals("zh-CN", StringComparison.OrdinalIgnoreCase)
                || language.StartsWith("zh-CN-", StringComparison.OrdinalIgnoreCase)
                || language.Equals("zh-SG", StringComparison.OrdinalIgnoreCase)
                || language.StartsWith("zh-SG-", StringComparison.OrdinalIgnoreCase);
        }
    }
}
