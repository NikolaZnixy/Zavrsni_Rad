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

        public async Task<ServiceHealthResult> PingAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var response = await _httpClient.GetAsync("models", cancellationToken);
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    return new ServiceHealthResult
                    {
                        Healthy = false,
                        Message = $"{(int)response.StatusCode}: {body}",
                        LatencyMs = stopwatch.ElapsedMilliseconds
                    };
                }

                return new ServiceHealthResult
                {
                    Healthy = true,
                    Message = $"Reachable, API key valid. Model: {_model}.",
                    LatencyMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                stopwatch.Stop();
                return new ServiceHealthResult
                {
                    Healthy = false,
                    Message = ex.Message,
                    LatencyMs = stopwatch.ElapsedMilliseconds
                };
            }
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
