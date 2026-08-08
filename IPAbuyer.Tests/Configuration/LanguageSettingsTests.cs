using IPAbuyer.Core.Configuration;
using Xunit;

namespace IPAbuyer.Tests.Configuration
{
    public sealed class LanguageSettingsTests
    {
        [Theory]
        [InlineData(null, LanguageSettings.AutoLanguage)]
        [InlineData("", LanguageSettings.AutoLanguage)]
        [InlineData("fr-FR", LanguageSettings.AutoLanguage)]
        [InlineData("ZH-hANS", LanguageSettings.ChineseLanguage)]
        [InlineData("en-us", LanguageSettings.EnglishLanguage)]
        public void Normalize_ReturnsSupportedCanonicalLanguage(string? language, string expected)
        {
            Assert.Equal(expected, LanguageSettings.Normalize(language));
        }

        [Theory]
        [InlineData(LanguageSettings.ChineseLanguage, "en-US", LanguageSettings.ChineseLanguage)]
        [InlineData(LanguageSettings.EnglishLanguage, "zh-CN", LanguageSettings.EnglishLanguage)]
        public void Resolve_ExplicitPreferenceTakesPrecedence(string preference, string systemLanguage, string expected)
        {
            Assert.Equal(expected, LanguageSettings.Resolve(preference, systemLanguage));
        }

        [Theory]
        [InlineData("zh-Hans")]
        [InlineData("zh-Hans-CN")]
        [InlineData("zh-CN")]
        [InlineData("zh-CN-x-private")]
        [InlineData("zh-SG")]
        [InlineData(" zh-CN ")]
        public void Resolve_AutoUsesSimplifiedChineseForSupportedSystemLanguage(string systemLanguage)
        {
            Assert.Equal(LanguageSettings.ChineseLanguage, LanguageSettings.Resolve(LanguageSettings.AutoLanguage, systemLanguage));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("en-GB")]
        [InlineData("zh-Hant")]
        [InlineData("zh-TW")]
        public void Resolve_AutoUsesEnglishForOtherSystemLanguages(string? systemLanguage)
        {
            Assert.Equal(LanguageSettings.EnglishLanguage, LanguageSettings.Resolve(LanguageSettings.AutoLanguage, systemLanguage));
        }
    }
}
