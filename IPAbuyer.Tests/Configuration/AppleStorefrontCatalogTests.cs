using IPAbuyer.Core.Configuration;
using Xunit;

namespace IPAbuyer.Tests.Configuration
{
    public sealed class AppleStorefrontCatalogTests
    {
        [Theory]
        [InlineData("CN")]
        [InlineData(" us ")]
        [InlineData("Tw")]
        public void Contains_AcceptsSupportedCodesIgnoringCaseAndWhitespace(string code)
        {
            Assert.True(AppleStorefrontCatalog.Contains(code));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("zz")]
        public void Contains_RejectsUnsupportedCodes(string? code)
        {
            Assert.False(AppleStorefrontCatalog.Contains(code));
        }

        [Fact]
        public void All_IsSortedAndContainsUniqueCodes()
        {
            AppleStorefront[] storefronts = AppleStorefrontCatalog.All.ToArray();

            Assert.NotEmpty(storefronts);
            Assert.Equal(storefronts.Length, storefronts.Select(storefront => storefront.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(
                storefronts.Select(storefront => storefront.Code).OrderBy(code => code, StringComparer.Ordinal),
                storefronts.Select(storefront => storefront.Code));
        }

        [Fact]
        public void SearchText_ContainsCodeAndEnglishName()
        {
            AppleStorefront storefront = Assert.Single(AppleStorefrontCatalog.All, item => item.Code == "us");

            Assert.Equal("us United States", storefront.SearchText);
        }
    }
}
