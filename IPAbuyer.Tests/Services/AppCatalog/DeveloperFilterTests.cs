using IPAbuyer.Core.Services.AppCatalog;
using Xunit;

namespace IPAbuyer.Tests.Services.AppCatalog
{
    public sealed class DeveloperFilterTests
    {
        [Fact]
        public void BuildOptions_IgnoresBlankNamesAndGroupsCaseInsensitively()
        {
            string?[] names = [null, "", "  ", " Acme ", "acme", "ACME", "Beta"];

            DeveloperFilterOption[] options = DeveloperFilter.BuildOptions(names).ToArray();

            Assert.Collection(
                options,
                option =>
                {
                    Assert.Equal("Acme", option.DisplayName);
                    Assert.Equal(3, option.Count);
                },
                option =>
                {
                    Assert.Equal("Beta", option.DisplayName);
                    Assert.Equal(1, option.Count);
                });
        }

        [Fact]
        public void BuildOptions_PreservesFirstNormalizedSpelling()
        {
            DeveloperFilterOption option = Assert.Single(DeveloperFilter.BuildOptions(["  Zebra Labs ", "zebra labs"]));

            Assert.Equal("Zebra Labs", option.DisplayName);
            Assert.Equal(2, option.Count);
        }

        [Fact]
        public void BuildOptions_OrdersSameCountsByDisplayName()
        {
            DeveloperFilterOption[] options = DeveloperFilter.BuildOptions(["Beta", "Acme", "Zebra"]).ToArray();

            Assert.Equal(["Acme", "Beta", "Zebra"], options.Select(option => option.DisplayName));
        }

        [Theory]
        [InlineData(null, null, true)]
        [InlineData("Acme", "", true)]
        [InlineData(" Acme ", "acme", true)]
        [InlineData("Acme", "Beta", false)]
        [InlineData(null, "Acme", false)]
        [InlineData("Acme", " Acme ", false)]
        public void Matches_AppliesSelectionAndDeveloperNormalization(string? developerName, string? selectedDeveloper, bool expected)
        {
            Assert.Equal(expected, DeveloperFilter.Matches(developerName, selectedDeveloper));
        }
    }
}
