using IPAbuyer.Core.Serialization;
using System.Text.Json;
using Xunit;

namespace IPAbuyer.Tests.Serialization
{
    public sealed class JsonPayloadTests
    {
        [Fact]
        public void EnumerateTokens_ParsesJsonLinesAndEmbeddedJson()
        {
            string payload = "debug line\n{\"first\":1}{\"second\":2}\ninfo: {\"third\":3}";

            JsonElement[] tokens = JsonPayload.EnumerateTokens(payload).ToArray();

            Assert.Equal(3, tokens.Length);
            Assert.True(JsonPayload.TryReadString(tokens[0], out string? first, "first"));
            Assert.Equal("1", first);
            Assert.True(JsonPayload.TryReadString(tokens[1], out string? second, "second"));
            Assert.Equal("2", second);
            Assert.True(JsonPayload.TryReadString(tokens[2], out string? third, "third"));
            Assert.Equal("3", third);
        }

        [Fact]
        public void EnumerateTokens_ParsesCompleteArrayAsSingleToken()
        {
            JsonElement token = Assert.Single(JsonPayload.EnumerateTokens("[{\"success\":true}]"));

            Assert.Equal(JsonValueKind.Array, token.ValueKind);
            Assert.False(JsonPayload.TryReadBoolean(token, "success", out _));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void EnumerateTokens_EmptyPayloadReturnsNoTokens(string? payload)
        {
            Assert.Empty(JsonPayload.EnumerateTokens(payload));
        }

        [Fact]
        public void TryReadBoolean_ReadsBooleanStringAndNumericValues()
        {
            Assert.True(JsonPayload.TryParseToken("{\"one\":true,\"two\":\"false\",\"three\":2,\"four\":0}", out JsonElement token));

            Assert.True(JsonPayload.TryReadBoolean(token, "ONE", out bool one));
            Assert.True(one);
            Assert.True(JsonPayload.TryReadBoolean(token, "two", out bool two));
            Assert.False(two);
            Assert.True(JsonPayload.TryReadBoolean(token, "three", out bool three));
            Assert.True(three);
            Assert.True(JsonPayload.TryReadBoolean(token, "four", out bool four));
            Assert.False(four);
        }

        [Fact]
        public void TryReadBoolean_RejectsMissingNullAndNonBooleanValues()
        {
            Assert.True(JsonPayload.TryParseToken("{\"empty\":null,\"text\":\"unknown\"}", out JsonElement token));

            Assert.False(JsonPayload.TryReadBoolean(token, "empty", out _));
            Assert.False(JsonPayload.TryReadBoolean(token, "text", out _));
            Assert.False(JsonPayload.TryReadBoolean(token, "missing", out _));
        }

        [Fact]
        public void TryReadString_UsesAliasesAndReturnsScalarValues()
        {
            Assert.True(JsonPayload.TryParseToken("{\"EMAIL\":\"user@example.com\",\"id\":42,\"enabled\":true}", out JsonElement token));

            Assert.True(JsonPayload.TryReadString(token, out string? email, "email", "eamil"));
            Assert.Equal("user@example.com", email);
            Assert.True(JsonPayload.TryReadString(token, out string? id, "id"));
            Assert.Equal("42", id);
            Assert.True(JsonPayload.TryReadString(token, out string? enabled, "enabled"));
            Assert.Equal("true", enabled);
        }

        [Theory]
        [InlineData("{\"price\":0}", "0.00")]
        [InlineData("{\"price\":-1}", "0.00")]
        [InlineData("{\"price\":12.5}", "12.5")]
        public void ReadScalarAsString_NormalizesNumericValues(string json, string expected)
        {
            Assert.True(JsonPayload.TryParseToken(json, out JsonElement token));
            JsonElement price = token.GetProperty("price");

            Assert.Equal(expected, JsonPayload.ReadScalarAsString(price));
        }

        [Fact]
        public void TryGetProperty_IgnoresCaseAndRejectsNonObjects()
        {
            Assert.True(JsonPayload.TryParseToken("{\"Name\":\"value\"}", out JsonElement token));

            Assert.True(JsonPayload.TryGetProperty(token, "name", out JsonElement matched));
            Assert.Equal("value", matched.GetString());
            Assert.False(JsonPayload.TryGetProperty(default, "name", out _));
        }

        [Fact]
        public void EnumerateTokens_SkipsLinesWithoutJson()
        {
            JsonElement[] tokens = JsonPayload.EnumerateTokens("plain\nwords only").ToArray();

            Assert.Empty(tokens);
        }
    }
}
