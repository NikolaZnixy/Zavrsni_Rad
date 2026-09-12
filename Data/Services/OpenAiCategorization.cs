using Data.Model.Data;
using Data.Model.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Data.Model.Data.CategorizationDtos;

namespace Data.Services
{
    public sealed class OpenAiCategorization : IAiCategorization
    {
        private const string SystemPrompt =
            "Classify bank transactions. Netflix, Spotify, Claude, and ChatGPT are subscriptions. " +
            "General retailers are groceries. Use luxury sparingly for unusual discretionary purchases.";

        private readonly HttpClient _httpClient;
        private readonly string _model;

        public OpenAiCategorization(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _model = configuration["OpenAI:Model"] ?? "gpt-5-nano";

            var apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("OpenAI:ApiKey is not configured.");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public string ProviderName => "OpenAI";

        public async Task<IReadOnlyList<CategorizationResult>> CategorizeAsync(
            IReadOnlyList<CategorizationInput> transactions,
            IReadOnlyList<string> categoryNames,
            CancellationToken cancellationToken = default)
        {
            if (transactions.Count == 0)
                return [];

            var indexedTransactions = transactions
                .Select((transaction, index) => new CompactTransaction(index, transaction.Description, transaction.Amount))
                .ToList();

            var request = new
            {
                model = _model,
                messages = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = JsonSerializer.Serialize(indexedTransactions) }
                },
                reasoning_effort = "minimal",
                response_format = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "transaction_categories",
                        strict = true,
                        schema = BuildResponseSchema(categoryNames)
                    }
                },
                max_completion_tokens = Math.Clamp((transactions.Count * 24) + 64, 128, 2048)
            };

            using var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"OpenAI /chat/completions failed ({(int)response.StatusCode}): {errorBody}",
                    null,
                    response.StatusCode);
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
                cancellationToken: cancellationToken);
            var content = completion?.Choices.FirstOrDefault()?.Message.Content;
            if (string.IsNullOrWhiteSpace(content))
                return [];

            CompactCategorizationResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<CompactCategorizationResponse>(content);
            }
            catch (JsonException)
            {
                return [];
            }

            if (parsed is null)
                return [];

            return parsed.Categorizations
                .Where(item => item.Index >= 0 && item.Index < transactions.Count)
                .GroupBy(item => item.Index)
                .Select(group =>
                {
                    var item = group.First();
                    return new CategorizationResult(transactions[item.Index].Id, item.Category);
                })
                .ToList();
        }

        /// <summary>
        /// Extended health probe: verifies the key and reachability via /models, checks the configured model
        /// is actually available, then runs one tiny completion to read live rate-limit and token quota
        /// headers (requests/tokens remaining plus their reset windows) and confirm inference works.
        /// </summary>
        public async Task<ServiceHealthResult> PingAsync(CancellationToken cancellationToken = default)
        {
            var result = new ServiceHealthResult { Provider = ProviderName };
            var total = Stopwatch.StartNew();

            try
            {
                var modelsStopwatch = Stopwatch.StartNew();
                using var modelsResponse = await _httpClient.GetAsync("models", cancellationToken);
                modelsStopwatch.Stop();

                result.Metrics.Add(new ServiceHealthMetric("Configured model", _model));
                result.Metrics.Add(new ServiceHealthMetric("/models latency", $"{modelsStopwatch.ElapsedMilliseconds} ms",
                    modelsStopwatch.ElapsedMilliseconds < 1500 ? "good" : "warn"));

                if (!modelsResponse.IsSuccessStatusCode)
                {
                    var body = await modelsResponse.Content.ReadAsStringAsync(cancellationToken);
                    total.Stop();
                    result.Healthy = false;
                    result.LatencyMs = total.ElapsedMilliseconds;
                    result.Message = $"/models returned {(int)modelsResponse.StatusCode}: {Shorten(body)}";
                    result.Metrics.Add(new ServiceHealthMetric("API key", DescribeKeyFailure(modelsResponse.StatusCode), "bad"));
                    return result;
                }

                result.Metrics.Add(new ServiceHealthMetric("API key", "Accepted", "good"));

                var models = await modelsResponse.Content.ReadFromJsonAsync<ModelListResponse>(cancellationToken: cancellationToken);
                var modelIds = models?.Data.Select(m => m.Id).ToList() ?? [];
                result.Metrics.Add(new ServiceHealthMetric("Models available", modelIds.Count.ToString()));

                var modelAvailable = modelIds.Contains(_model, StringComparer.OrdinalIgnoreCase);
                result.Metrics.Add(new ServiceHealthMetric(
                    "Model reachable",
                    modelAvailable ? "Yes" : "Not listed for this key",
                    modelAvailable ? "good" : "warn"));

                // One deliberately tiny completion: proves inference actually works for the configured model
                // and is the only way OpenAI exposes live quota - the rate-limit headers come back on it.
                var completionStopwatch = Stopwatch.StartNew();
                using var probeResponse = await _httpClient.PostAsJsonAsync(
                    "chat/completions",
                    new
                    {
                        model = _model,
                        messages = new object[] { new { role = "user", content = "ping" } },
                        max_completion_tokens = 16
                    },
                    cancellationToken);
                completionStopwatch.Stop();

                result.Metrics.Add(new ServiceHealthMetric("Completion latency", $"{completionStopwatch.ElapsedMilliseconds} ms",
                    completionStopwatch.ElapsedMilliseconds < 4000 ? "good" : "warn"));

                AddRateLimitMetrics(result, probeResponse);

                total.Stop();
                result.LatencyMs = total.ElapsedMilliseconds;

                if (!probeResponse.IsSuccessStatusCode)
                {
                    var body = await probeResponse.Content.ReadAsStringAsync(cancellationToken);
                    result.Healthy = false;
                    result.Message = (int)probeResponse.StatusCode == 429
                        ? $"Rate limited or out of quota (429): {Shorten(body)}"
                        : $"Test completion failed ({(int)probeResponse.StatusCode}): {Shorten(body)}";
                    return result;
                }

                var completion = await probeResponse.Content.ReadFromJsonAsync<ChatCompletionResponse>(
                    cancellationToken: cancellationToken);

                if (completion?.Usage is { } usage)
                {
                    result.Metrics.Add(new ServiceHealthMetric(
                        "Probe token usage",
                        $"{usage.TotalTokens} total ({usage.PromptTokens} prompt / {usage.CompletionTokens} completion)"));
                }

                result.Healthy = true;
                result.Message = $"Reachable, key valid, {_model} responded to a live completion.";
                return result;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                total.Stop();
                result.Healthy = false;
                result.LatencyMs = total.ElapsedMilliseconds;
                result.Message = ex.Message;
                return result;
            }
        }

        private static void AddRateLimitMetrics(ServiceHealthResult result, HttpResponseMessage response)
        {
            AddHeaderMetric(result, response, "x-ratelimit-limit-requests", "Request limit");
            AddHeaderMetric(result, response, "x-ratelimit-remaining-requests", "Requests remaining", limitHeader: "x-ratelimit-limit-requests");
            AddHeaderMetric(result, response, "x-ratelimit-reset-requests", "Requests reset in");
            AddHeaderMetric(result, response, "x-ratelimit-limit-tokens", "Token limit");
            AddHeaderMetric(result, response, "x-ratelimit-remaining-tokens", "Tokens remaining", limitHeader: "x-ratelimit-limit-tokens");
            AddHeaderMetric(result, response, "x-ratelimit-reset-tokens", "Tokens reset in");

            if (response.Headers.TryGetValues("retry-after", out var retryAfter))
                result.Metrics.Add(new ServiceHealthMetric("Retry after", $"{retryAfter.First()} s", "bad"));
        }

        private static void AddHeaderMetric(
            ServiceHealthResult result,
            HttpResponseMessage response,
            string header,
            string label,
            string? limitHeader = null)
        {
            if (!response.Headers.TryGetValues(header, out var values))
                return;

            var value = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (limitHeader is not null
                && response.Headers.TryGetValues(limitHeader, out var limitValues)
                && long.TryParse(value, out var remaining)
                && long.TryParse(limitValues.FirstOrDefault(), out var limit)
                && limit > 0)
            {
                var fraction = (double)remaining / limit;
                var status = fraction switch
                {
                    < 0.1 => "bad",
                    < 0.3 => "warn",
                    _ => "good"
                };
                result.Metrics.Add(new ServiceHealthMetric(label, $"{remaining:N0} of {limit:N0} ({fraction:P0})", status));
                return;
            }

            result.Metrics.Add(new ServiceHealthMetric(label, value));
        }

        private static string DescribeKeyFailure(System.Net.HttpStatusCode statusCode) => (int)statusCode switch
        {
            401 => "Rejected (invalid or revoked)",
            403 => "Rejected (no access to this resource)",
            429 => "Accepted, but rate limited or out of quota",
            _ => "Unknown"
        };

        private static string Shorten(string value)
        {
            value = value.Replace('\n', ' ').Trim();
            return value.Length <= 300 ? value : value[..300] + "…";
        }

        private static object BuildResponseSchema(IReadOnlyList<string> categoryNames)
        {
            var allowedCategories = categoryNames.Cast<object?>().Append(null).ToArray();

            return new
            {
                type = "object",
                properties = new
                {
                    c = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                i = new { type = "integer" },
                                c = new { type = new[] { "string", "null" }, @enum = allowedCategories }
                            },
                            required = new[] { "i", "c" },
                            additionalProperties = false
                        }
                    }
                },
                required = new[] { "c" },
                additionalProperties = false
            };
        }

        private sealed record CompactTransaction(
            [property: JsonPropertyName("i")] int Index,
            [property: JsonPropertyName("d")] string Description,
            [property: JsonPropertyName("a")] decimal Amount);

        private sealed class ChatCompletionResponse
        {
            [JsonPropertyName("choices")]
            public List<ChatChoice> Choices { get; set; } = [];

            [JsonPropertyName("usage")]
            public TokenUsage? Usage { get; set; }
        }

        private sealed class TokenUsage
        {
            [JsonPropertyName("prompt_tokens")]
            public int PromptTokens { get; set; }

            [JsonPropertyName("completion_tokens")]
            public int CompletionTokens { get; set; }

            [JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }
        }

        private sealed class ModelListResponse
        {
            [JsonPropertyName("data")]
            public List<ModelInfo> Data { get; set; } = [];
        }

        private sealed class ModelInfo
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = "";
        }

        private sealed class ChatChoice
        {
            [JsonPropertyName("message")]
            public ChatMessage Message { get; set; } = new();
        }

        private sealed class ChatMessage
        {
            [JsonPropertyName("content")]
            public string Content { get; set; } = "";
        }

        private sealed class CompactCategorizationResponse
        {
            [JsonPropertyName("c")]
            public List<CompactCategorization> Categorizations { get; set; } = [];
        }

        private sealed class CompactCategorization
        {
            [JsonPropertyName("i")]
            public int Index { get; set; }

            [JsonPropertyName("c")]
            public string? Category { get; set; }
        }
    }
}
