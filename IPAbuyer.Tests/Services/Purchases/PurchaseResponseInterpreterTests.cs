using IPAbuyer.Core.Services.Purchases;
using Xunit;

namespace IPAbuyer.Tests.Services.Purchases
{
    public sealed class PurchaseResponseInterpreterTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("{\"success\":false}")]
        [InlineData("unrecognized failure")]
        public void Interpret_UnsuccessfulPayloadReturnsFailed(string? payload)
        {
            Assert.Equal(PurchaseOutcome.Failed, PurchaseResponseInterpreter.Interpret(payload));
        }

        [Theory]
        [InlineData("{\"success\":true}")]
        [InlineData("{\"success\":\"true\"}")]
        [InlineData("{\"success\":1}")]
        [InlineData("debug\n{\"success\":true}")]
        public void Interpret_SuccessPayloadReturnsPurchased(string payload)
        {
            Assert.Equal(PurchaseOutcome.Purchased, PurchaseResponseInterpreter.Interpret(payload));
        }

        [Fact]
        public void Interpret_AlreadyOwnedTakesPrecedenceOverSuccess()
        {
            PurchaseOutcome outcome = PurchaseResponseInterpreter.Interpret("{\"alreadyOwned\":true,\"success\":true}");

            Assert.Equal(PurchaseOutcome.AlreadyOwned, outcome);
        }

        [Theory]
        [InlineData("failed to purchase item with param 'STDQ'")]
        [InlineData("FAILED TO PURCHASE ITEM WITH PARAM 'stdq'")]
        public void Interpret_StdqFailureRequiresOwnedConfirmation(string payload)
        {
            Assert.Equal(PurchaseOutcome.NeedsOwnedConfirmation, PurchaseResponseInterpreter.Interpret(payload));
        }

        [Fact]
        public void Interpret_AlreadyOwnedDetectedInEmbeddedJson()
        {
            PurchaseOutcome outcome = PurchaseResponseInterpreter.Interpret("debug\n{\"alreadyOwned\":true}");

            Assert.Equal(PurchaseOutcome.AlreadyOwned, outcome);
        }

        [Fact]
        public void Interpret_AlreadyOwnedReadsStringValuesThroughJsonTokens()
        {
            PurchaseOutcome outcome = PurchaseResponseInterpreter.Interpret("{\"alreadyOwned\": \"true\"}");

            Assert.Equal(PurchaseOutcome.AlreadyOwned, outcome);
        }

        [Fact]
        public void Interpret_FalseFlagsDoNotTriggerOwnedOrPurchased()
        {
            PurchaseOutcome outcome = PurchaseResponseInterpreter.Interpret("{\"alreadyOwned\":false,\"success\":false}");

            Assert.Equal(PurchaseOutcome.Failed, outcome);
        }
    }
}
