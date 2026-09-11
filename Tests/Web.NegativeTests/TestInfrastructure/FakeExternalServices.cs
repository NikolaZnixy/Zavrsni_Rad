using Data.Services;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace Web.NegativeTests.TestInfrastructure
{
    /// <summary>
    /// Builds real external service implementations wired to a FakeHttpMessageHandler instead of the
    /// network, so tests exercise serialization and failure handling deterministically.
    /// </summary>
    public static class FakeExternalServices
    {
        // A syntactically valid (but throwaway) RSA key pair, generated fresh per test run, so
        // EnableBankingClient's JWT signing step succeeds and the test can focus purely on how the
        // controller reacts to the HTTP response/failure that comes back from Enable Banking itself.
        public static string GenerateRsaPrivateKeyPem()
        {
            using var rsa = RSA.Create(2048);
            return rsa.ExportRSAPrivateKeyPem();
        }

        public static EnableBankingClient BuildEnableBankingClient(FakeHttpMessageHandler handler, string? privateKeyPem = null)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["EnableBanking:ApplicationId"] = "test-application-id",
                    ["EnableBanking:PrivateKeyPem"] = privateKeyPem ?? GenerateRsaPrivateKeyPem(),
                })
                .Build();

            var httpClient = FakeHttpMessageHandler.BuildClient(handler, new Uri("https://api.enablebanking.test/"));
            return new EnableBankingClient(httpClient, config);
        }

        public static OpenAiCategorization BuildOpenAiCategorization(FakeHttpMessageHandler handler, string? apiKey = null)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenAI:ApiKey"] = apiKey ?? "test-api-key",
                    ["OpenAI:Model"] = "gpt-5-nano",
                })
                .Build();

            var httpClient = FakeHttpMessageHandler.BuildClient(handler, new Uri("https://api.openai.test/v1/"));
            return new OpenAiCategorization(httpClient, config);
        }
    }
}
