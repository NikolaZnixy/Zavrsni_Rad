using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using static Data.Model.Data.EnableBankingDtos;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Data.Services
{
    public class EnableBankingClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _applicationId;
        private readonly string _privateKeyPem;
        public EnableBankingClient(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _applicationId = config["EnableBanking:ApplicationId"]!;
            _privateKeyPem = config["EnableBanking:PrivateKeyPem"]!;
        }

        public async Task<AspspsResponse> GetAspspsAsync(string country)
        {
            SetAuthHeader();
            var response = await _httpClient.GetAsync($"aspsps?country={country}");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AspspsResponse>())!;
        }

        public async Task<AuthResponse> StartAuthorizationAsync(string aspspName, string aspspCountry, string redirectUrl, string state)
        {
            SetAuthHeader();

            var request = new StartAuthorizationRequest
            {
                Access = new AccessValidity { ValidUntil = DateTimeOffset.UtcNow.AddDays(90).ToString("o") },
                Aspsp = new AspspRef { Name = aspspName, Country = aspspCountry },
                State = state,
                RedirectUrl = redirectUrl
            };

            var response = await _httpClient.PostAsJsonAsync("auth", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Enable Banking /auth failed ({(int)response.StatusCode}): {errorBody}");
            }

            return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        }

        public async Task<SessionResponse> CreateSessionAsync(string code)
        {
            SetAuthHeader();

            var request = new CreateSessionRequest { Code = code };
            var response = await _httpClient.PostAsJsonAsync("sessions", request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<SessionResponse>())!;
        }

        public async Task<TransactionsResponse> GetTransactionsAsync(Guid accountUid, DateOnly? dateFrom = null, DateOnly? dateTo = null)
        {
            SetAuthHeader();

            var effectiveDateFrom = dateFrom ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6));
            var effectiveDateTo = dateTo ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var url = $"accounts/{accountUid}/transactions?date_from={effectiveDateFrom:yyyy-MM-dd}&date_to={effectiveDateTo:yyyy-MM-dd}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var rawJson = await response.Content.ReadAsStringAsync();
            Console.WriteLine(rawJson);

            return System.Text.Json.JsonSerializer.Deserialize<TransactionsResponse>(rawJson)!;
        }


        private string CreateJwt()
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(_privateKeyPem);

            var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)
            {
                CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
            };

            var now = DateTimeOffset.UtcNow;
            var handler = new JwtSecurityTokenHandler();
            var token = new JwtSecurityToken(
                  header: new JwtHeader(signingCredentials)
                  {
                      ["kid"] = _applicationId
                  },
                  payload: new JwtPayload
                  {
                { "iss", "enablebanking.com" },
                { "aud", "api.enablebanking.com" },
                { "iat", now.ToUnixTimeSeconds() },
                { "exp", now.AddHours(1).ToUnixTimeSeconds() }
                  });

            return handler.WriteToken(token);
        }

        private void SetAuthHeader() =>
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt());
    }
}
