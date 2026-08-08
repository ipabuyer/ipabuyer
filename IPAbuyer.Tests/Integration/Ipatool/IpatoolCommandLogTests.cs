using IPAbuyer.Core.Integration.Ipatool;
using Xunit;

namespace IPAbuyer.Tests.Integration.Ipatool
{
    public sealed class IpatoolCommandLogTests
    {
        [Fact]
        public void Sanitize_RedactsSensitiveJsonProperties()
        {
            string value = "{\"password\":\"secret\",\"authCode\":\"123456\",\"keychain-passphrase\":\"pass\",\"PasswordToken\":\"token\",\"email\":\"user@example.com\"}";

            string sanitized = IpatoolCommandLog.Sanitize(value);

            Assert.DoesNotContain("secret", sanitized);
            Assert.DoesNotContain("123456", sanitized);
            Assert.DoesNotContain("\"pass\"", sanitized);
            Assert.DoesNotContain("\"token\"", sanitized);
            Assert.Contains("\"password\":\"***\"", sanitized);
            Assert.Contains("\"email\":\"user@example.com\"", sanitized);
        }

        [Fact]
        public void Preview_SanitizesBeforeTruncating()
        {
            string value = new string('a', 220) + " {\"passphrase\":\"secret\"}";

            string preview = IpatoolCommandLog.Preview(value);

            Assert.DoesNotContain("secret", preview);
            Assert.True(preview.Length <= 203);
            Assert.EndsWith("...", preview, StringComparison.Ordinal);
        }
    }
}
