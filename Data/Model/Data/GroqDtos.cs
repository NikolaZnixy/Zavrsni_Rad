using System.Text.Json.Serialization;

namespace Data.Model.Data
{
    public class GroqDtos
    {
        // POST /openai/v1/chat/completions
        public class GroqChatRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = "";

            [JsonPropertyName("messages")]
            public List<GroqMessage> Messages { get; set; } = new();

            [JsonPropertyName("response_format")]
            public GroqResponseFormat ResponseFormat { get; set; } = new();

            [JsonPropertyName("temperature")]
            public double Temperature { get; set; }
        }

        public class GroqMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = "";

            [JsonPropertyName("content")]
            public string Content { get; set; } = "";
        }

        public class GroqResponseFormat
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "json_object";
        }

        public class GroqChatCompletionResponse
        {
            [JsonPropertyName("choices")]
            public List<GroqChoice> Choices { get; set; } = new();
        }

        public class GroqChoice
        {
            [JsonPropertyName("message")]
            public GroqMessage Message { get; set; } = new();
        }

        // What we send Groq about each transaction - deliberately minimal, no account/IBAN data.
        public class TransactionForCategorization
        {
            [JsonPropertyName("id")]
            public Guid Id { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("amount")]
            public decimal Amount { get; set; }

            [JsonPropertyName("currency")]
            public string Currency { get; set; } = "";

            [JsonPropertyName("date")]
            public string Date { get; set; } = "";
        }

        // The strict JSON shape we require back from the model (see GroqClient's system prompt).
        public class GroqCategorizationResponse
        {
            [JsonPropertyName("categorizations")]
            public List<GroqCategorization> Categorizations { get; set; } = new();
        }

        public class GroqCategorization
        {
            [JsonPropertyName("id")]
            public Guid Id { get; set; }

            [JsonPropertyName("category")]
            public string? Category { get; set; }
        }
    }
}
