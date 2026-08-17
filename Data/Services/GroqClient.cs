using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using static Data.Model.Data.GroqDtos;

namespace Data.Services
{
    /// <summary>
    /// Talks to Groq's OpenAI-compatible chat-completions endpoint to categorize bank transactions
    /// into a fixed, closed set of categories. Never guesses outside that set - the caller is
    /// responsible for validating the response before trusting it (see BankController).
    /// </summary>
    public class GroqClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;

        public GroqClient(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            var apiKey = config["Groq:ApiKey"]!;
            _model = config["Groq:Model"] ?? "llama-3.3-70b-versatile";
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        /// <summary>
        /// Asks Groq to categorize a batch of transactions into one of <paramref name="categoryNames"/>.
        /// Returns null if the model's response couldn't be parsed at all - callers should treat that
        /// the same as "nothing categorized this round" rather than failing the whole request.
        /// </summary>
        public async Task<GroqCategorizationResponse?> CategorizeTransactionsAsync(
            IReadOnlyList<TransactionForCategorization> transactions,
            IReadOnlyList<string> categoryNames)
        {
            var request = new GroqChatRequest
            {
                Model = _model,
                Temperature = 0,
                ResponseFormat = new GroqResponseFormat { Type = "json_object" },
                Messages = new List<GroqMessage>
                {
                    new() { Role = "system", Content = BuildSystemPrompt(categoryNames) },
                    new() { Role = "user", Content = JsonSerializer.Serialize(transactions) }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("chat/completions", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Groq /chat/completions failed ({(int)response.StatusCode}): {errorBody}");
            }

            var completion = await response.Content.ReadFromJsonAsync<GroqChatCompletionResponse>();
            var content = completion?.Choices.FirstOrDefault()?.Message.Content;

            if (string.IsNullOrWhiteSpace(content))
                return null;

            try
            {
                return JsonSerializer.Deserialize<GroqCategorizationResponse>(content);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string BuildSystemPrompt(IReadOnlyList<string> categoryNames) => $$"""
            This is a fintech app that manages a user's bank transactions. You will be given a JSON array of
            that user's transactions from the last several days. Each transaction has an id, description,
            amount (negative means money out, positive means money in), currency, and date.

            Your job is to categorize each transaction by reading its description (using amount and date only
            as supporting context) and assigning it to the single best-fitting category.

            The ONLY valid categories are: {{string.Join(", ", categoryNames)}}.
            Do not invent new categories and do not change the spelling or casing of the categories above.
            If a transaction does not clearly and confidently fit any of these categories, set its category
            to null instead of guessing.

            Anything that involves Netflix, spotify, claude code, chatgpt can be rendered as subscription.
            Anything that involves parking can be rendered as sc+ubscription.
            Any store that includes generalized set of articles can be rendered as groceries.
            Luxury category should be the least common one with luxury being something like ryanair, H&M purchases and so on.

            Respond with ONLY a JSON object of this exact shape, with exactly one entry per transaction id you
            were given:
            {"categorizations":[{"id":"<transaction id>","category":"<one of the categories above, or null>"}]}
            """;
    }
}
