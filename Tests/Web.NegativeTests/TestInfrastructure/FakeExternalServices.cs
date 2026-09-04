using Data.Services;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace Web.NegativeTests.TestInfrastructure
{
    /// <summary>
    /// Builds real EnableBankingClient/GroqClient instances wired to a FakeHttpMessageHandler instead
    /// of the real network. EnableBankingClient and GroqClient aren't behind interfaces and have no
    /// virtual members, so they can't be mocked with Moq directly - swapping out the HttpClient's
    /// handler is the seam the production code already exposes for this.
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

        public static GroqClient BuildGroqClient(FakeHttpMessageHandler handler, string? apiKey = null)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Groq:ApiKey"] = apiKey ?? "test-api-key",
                    ["Groq:Model"] = "test-model",
                })
                .Build();

            var httpClient = FakeHttpMessageHandler.BuildClient(handler, new Uri("https://api.groq.test/"));
            return new GroqClient(httpClient, config);
        }
    }
}
