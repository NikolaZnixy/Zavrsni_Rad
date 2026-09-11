using Data.Model.Data;
using System.Net;
using System.Text;
using System.Text.Json;
using Web.NegativeTests.TestInfrastructure;

namespace Web.NegativeTests
{
    public class OpenAiCategorizationTests
    {
        [Fact]
        public async Task CategorizeAsync_UsesLowCostCompactStructuredRequest_AndMapsIndexesToIds()
        {
            string? requestBody = null;
            var handler = FakeHttpMessageHandler.RespondingAsync(async (request, cancellationToken) =>
            {
                requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                const string response = """
                    {"choices":[{"message":{"content":"{\"c\":[{\"i\":0,\"c\":\"groceries\"}]}"}}]}
                    """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(response, Encoding.UTF8, "application/json")
                };
            });
            var service = FakeExternalServices.BuildOpenAiCategorization(handler);
            var transactionId = Guid.NewGuid();

            var result = await service.CategorizeAsync(
                [new CategorizationDtos.CategorizationInput(transactionId, "Local market", -12.34m)],
                ["groceries", "subscriptions"]);

            var categorization = Assert.Single(result);
            Assert.Equal(transactionId, categorization.Id);
            Assert.Equal("groceries", categorization.Category);

            using var requestJson = JsonDocument.Parse(requestBody!);
            var root = requestJson.RootElement;
            Assert.Equal("gpt-5-nano", root.GetProperty("model").GetString());
            Assert.Equal("minimal", root.GetProperty("reasoning_effort").GetString());
            Assert.Equal("json_schema", root.GetProperty("response_format").GetProperty("type").GetString());
            Assert.True(root.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean());

            var userPayload = root.GetProperty("messages")[1].GetProperty("content").GetString()!;
            Assert.Contains("\"i\":0", userPayload);
            Assert.Contains("\"d\":\"Local market\"", userPayload);
            Assert.Contains("\"a\":-12.34", userPayload);
            Assert.DoesNotContain(transactionId.ToString(), userPayload);
        }

        [Fact]
        public async Task NoOpCategorization_ReturnsNoResults()
        {
            var service = new Data.Services.NoOpCategorizationService();

            var result = await service.CategorizeAsync(
                [new CategorizationDtos.CategorizationInput(Guid.NewGuid(), "Merchant", -1m)],
                ["groceries"]);

            Assert.Empty(result);
        }
    }
}
