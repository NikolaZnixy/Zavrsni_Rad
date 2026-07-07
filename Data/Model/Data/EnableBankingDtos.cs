using System.Text.Json.Serialization;

namespace Data.Model.Data
{
    public class EnableBankingDtos
    {
        public class AspspsResponse
        {
            [JsonPropertyName("aspsps")]
            public List<Aspsp> Aspsps { get; set; } = new();
        }

        public class Aspsp
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("country")]
            public string Country { get; set; } = "";

            [JsonPropertyName("logo")]
            public string? Logo { get; set; }

            [JsonPropertyName("bic")]
            public string? Bic { get; set; }
        }

        // POST /auth
        public class StartAuthorizationRequest
        {
            [JsonPropertyName("access")]
            public AccessValidity Access { get; set; } = new();

            [JsonPropertyName("aspsp")]
            public AspspRef Aspsp { get; set; } = new();

            [JsonPropertyName("state")]
            public string State { get; set; } = "";

            [JsonPropertyName("redirect_url")]
            public string RedirectUrl { get; set; } = "";

            [JsonPropertyName("psu_type")]
            public string PsuType { get; set; } = "personal";
        }

        public class AccessValidity
        {
            [JsonPropertyName("valid_until")]
            public string ValidUntil { get; set; } = "";
        }

        public class AspspRef
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("country")]
            public string Country { get; set; } = "";
        }

        public class AuthResponse
        {
            [JsonPropertyName("url")]
            public string Url { get; set; } = "";

            [JsonPropertyName("authorization_id")]
            public string AuthorizationId { get; set; } = "";
        }

        // POST /sessions
        public class CreateSessionRequest
        {
            [JsonPropertyName("code")]
            public string Code { get; set; } = "";
        }

        public class SessionResponse
        {
            [JsonPropertyName("session_id")]
            public string SessionId { get; set; } = "";

            [JsonPropertyName("accounts")]
            public List<AccountInfo> Accounts { get; set; } = new();
        }

        public class AccountInfo
        {
            [JsonPropertyName("uid")]
            public Guid Uid { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("currency")]
            public string? Currency { get; set; }
        }

        // GET /accounts/{uid}/transactions
        public class TransactionsResponse
        {
            [JsonPropertyName("transactions")]
            public List<Transaction> Transactions { get; set; } = new();

            [JsonPropertyName("continuation_key")]
            public string? ContinuationKey { get; set; }
        }

        public class Transaction
        {
            [JsonPropertyName("transaction_id")]
            public string? TransactionId { get; set; }

            [JsonPropertyName("transaction_amount")]
            public TransactionAmount TransactionAmount { get; set; } = new();

            [JsonPropertyName("booking_date")]
            public string? BookingDate { get; set; }

            [JsonPropertyName("credit_debit_indicator")]
            public string? CreditDebitIndicator { get; set; }

            [JsonPropertyName("remittance_information")]
            public List<string>? RemittanceInformation { get; set; }
        }

        public class TransactionAmount
        {
            [JsonPropertyName("currency")]
            public string Currency { get; set; } = "";

            [JsonPropertyName("amount")]
            public string Amount { get; set; } = "";
        }
    }
}
