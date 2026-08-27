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

        /// <summary>
        /// Lightweight health check - GET /application returns this app's own registered metadata, which
        /// only succeeds if the JWT signing and application id are valid. Touches no bank/session, so it
        /// can't contribute to a per-bank rate limit.
        /// </summary>
        public async Task<Data.Model.Data.ServiceHealthResult> PingAsync()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                SetAuthHeader();
                var response = await _httpClient.GetAsync("application");
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return new Data.Model.Data.ServiceHealthResult { Healthy = false, Message = $"{(int)response.StatusCode}: {body}", LatencyMs = stopwatch.ElapsedMilliseconds };
                }

                return new Data.Model.Data.ServiceHealthResult { Healthy = true, Message = "Reachable, credentials valid.", LatencyMs = stopwatch.ElapsedMilliseconds };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new Data.Model.Data.ServiceHealthResult { Healthy = false, Message = ex.Message, LatencyMs = stopwatch.ElapsedMilliseconds };
            }
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

        /// <summary>
        /// Fetches a single page of transactions. Passing no dateFrom/dateTo does NOT mean "as much history
        /// as possible" - Enable Banking's own default for an omitted date_from is a short recent window
        /// (observed ~7 days for at least one ASPSP), so callers that actually want a wide window need to
        /// pass dateFrom explicitly (see BankController.SyncTransactions). Pass <paramref name="continuationKey"/>
        /// (from a previous page's ContinuationKey) to walk subsequent pages.
        /// </summary>
        // Enable Banking wraps upstream bank failures as 400 {"error":"ASPSP_ERROR"} - the bank's own connector
        // choked, not our request. These are usually transient (flaky sandbox/bank connections), so worth a
        // few retries before giving up. A handful of real 5xx responses are treated the same way.
        private static readonly TimeSpan[] AspspRetryDelays = { TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(3) };

        public async Task<TransactionsResponse> GetTransactionsAsync(Guid accountUid, DateOnly? dateFrom = null, DateOnly? dateTo = null, string? continuationKey = null)
        {
            var query = new List<string>();
            if (dateFrom is { } from) query.Add($"date_from={from:yyyy-MM-dd}");
            if (dateTo is { } to) query.Add($"date_to={to:yyyy-MM-dd}");
            if (continuationKey is not null) query.Add($"continuation_key={Uri.EscapeDataString(continuationKey)}");

            var url = $"accounts/{accountUid}/transactions" + (query.Count > 0 ? "?" + string.Join("&", query) : "");

            string rawJson;
            for (var attempt = 0; ; attempt++)
            {
                SetAuthHeader();
                var response = await _httpClient.GetAsync(url);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    rawJson = body;
                    break;
                }

                var isTransientAspspError = body.Contains("\"ASPSP_ERROR\"") || (int)response.StatusCode >= 500;
                if (!isTransientAspspError || attempt >= AspspRetryDelays.Length)
                    throw new HttpRequestException($"Enable Banking GET {url} failed ({(int)response.StatusCode}): {body}");

                Console.WriteLine($"Enable Banking transactions fetch hit a transient ASPSP error (attempt {attempt + 1}/{AspspRetryDelays.Length + 1}), retrying: {body}");
                await Task.Delay(AspspRetryDelays[attempt]);
            }

            var prettyJson = System.Text.Json.JsonSerializer.Serialize(
                System.Text.Json.JsonDocument.Parse(rawJson).RootElement,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine("===== Enable Banking raw transactions response =====");
            Console.WriteLine(prettyJson);
            Console.WriteLine("===== End raw transactions response =====");

            return System.Text.Json.JsonSerializer.Deserialize<TransactionsResponse>(rawJson)!;
        }

        /// <summary>
        /// Fetches the account's full available transaction history by walking every continuation_key page
        /// until the ASPSP stops returning one. This is what "fetch as much as possible" means in practice -
        /// Enable Banking itself still caps history to whatever the bank/consent allows, but nothing on our
        /// side truncates it further.
        /// </summary>
        public async Task<List<Transaction>> GetAllTransactionsAsync(Guid accountUid, DateOnly? dateFrom = null, DateOnly? dateTo = null)
        {
            var all = new List<Transaction>();
            string? continuationKey = null;

            do
            {
                var page = await GetTransactionsAsync(accountUid, dateFrom, dateTo, continuationKey);
                all.AddRange(page.Transactions);
                continuationKey = page.ContinuationKey;
            } while (continuationKey is not null);

            return all;
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
