using Data.Model;
using Data.Model.Data;
using Data.Model.Interfaces;
using Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using static Data.Model.Data.CategorizationDtos;

namespace Web.Controllers.Api
{
    [ApiController]
    [Route("api/bank")]
    public class BankController : ControllerBase
    {
        private readonly EnableBankingClient _client;
        private readonly ICategorizationService _categorizationService;
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public BankController(EnableBankingClient client, ICategorizationService categorizationService, AppDbContext db, UserManager<AppUser> userManager, IWebHostEnvironment env)
        {
            _client = client;
            _categorizationService = categorizationService;
            _db = db;
            _userManager = userManager;
            _env = env;
        }

        private record LinkState(string UserId, string DisplayName, string AspspName, string Country);

        [HttpGet("connect")]
        [Authorize]
        public async Task<IActionResult> Connect(string bank, string country, string displayName, [FromServices] IConfiguration config)
        {
            var redirectUrl = config["EnableBanking:RedirectUrl"]!;
            var userId = _userManager.GetUserId(User)!;

            var state = Convert.ToBase64String(
                JsonSerializer.SerializeToUtf8Bytes(new LinkState(userId, displayName, bank, country)));

            var auth = await _client.StartAuthorizationAsync(bank, country, redirectUrl, state);
            return Redirect(auth.Url);
        }

        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback(string code, string state)
        {
            var linkState = JsonSerializer.Deserialize<LinkState>(Convert.FromBase64String(state))!;

            var session = await _client.CreateSessionAsync(code);
            var consentValidUntil = DateTimeOffset.UtcNow.AddDays(90);
            var linkedAt = DateTimeOffset.UtcNow;

            for (var i = 0; i < session.Accounts.Count; i++)
            {
                var account = session.Accounts[i];
                var displayName = i == 0 ? linkState.DisplayName : $"{linkState.DisplayName} ({i + 1})";

                _db.LinkedBankAccounts.Add(new LinkedBankAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = linkState.UserId,
                    DisplayName = displayName,
                    AspspName = linkState.AspspName,
                    Country = linkState.Country,
                    EnableBankingAccountId = account.Uid,
                    Iban = account.AccountId?.Iban ?? string.Empty,
                    ConsentValidUntil = consentValidUntil,
                    LinkedAt = linkedAt
                });
            }

            await _db.SaveChangesAsync();

            return RedirectToAction("Accounts", "Transactions");
        }

        [HttpDelete("{linkedAccountId}")]
        [Authorize]
        public async Task<IActionResult> Disconnect(Guid linkedAccountId)
        {
            var userId = _userManager.GetUserId(User)!;
            var account = await _db.LinkedBankAccounts
                .FirstOrDefaultAsync(a => a.Id == linkedAccountId && a.UserId == userId);

            if (account is null)
                return NotFound();

            var transactions = _db.BankAccountTransactions.Where(t => t.LinkedBankAccountId == linkedAccountId);
            _db.BankAccountTransactions.RemoveRange(transactions);
            _db.LinkedBankAccounts.Remove(account);

            await _db.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("transactions/{linkedAccountId}/sync")]
        [Authorize]
        public async Task<IActionResult> SyncTransactions(Guid linkedAccountId)
        {
            var userId = _userManager.GetUserId(User)!;
            var account = await _db.LinkedBankAccounts
                .FirstOrDefaultAsync(a => a.Id == linkedAccountId && a.UserId == userId);

            if (account is null)
                return NotFound();

            // Enable Banking's own default when date_from is omitted is a short recent window (looks like
            // ~7 days for this ASPSP), not "as much history as the bank allows" - so ask explicitly for as
            // far back as we want instead of relying on an unspecified default. The bank/consent may still
            // cap this shorter (PSD2 commonly limits to 90 days unless extended history was granted).
            var dateFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6));
            var dateTo = DateOnly.FromDateTime(DateTime.UtcNow);

            List<Data.Model.Data.EnableBankingDtos.Transaction> fetched;
            try
            {
                fetched = await _client.GetAllTransactionsAsync(account.EnableBankingAccountId, dateFrom, dateTo);
            }
            catch (HttpRequestException ex)
            {
                // The bank's own connector failed on Enable Banking's side (ASPSP_ERROR) even after retries,
                // or some other upstream failure - surface it as a normal error response instead of crashing
                // the request, so the UI can show it and the user can just try again.
                return StatusCode(502, new { error = "Your bank couldn't return transactions right now. Try again in a bit.", detail = ex.Message });
            }

            var existingExternalIds = await _db.BankAccountTransactions
                .Where(t => t.LinkedBankAccountId == linkedAccountId && t.ExternalTransactionId != null)
                .Select(t => t.ExternalTransactionId)
                .ToListAsync();
            var existingIdSet = existingExternalIds.ToHashSet();

            var added = 0;
            foreach (var transaction in fetched)
            {
                var externalId = transaction.TransactionId ?? transaction.EntryReference;

                if (externalId is not null && existingIdSet.Contains(externalId))
                    continue;

                if (!DateOnly.TryParse(transaction.BookingDate, CultureInfo.InvariantCulture, out var bookingDate))
                    continue;

                var amount = decimal.Parse(transaction.TransactionAmount.Amount, CultureInfo.InvariantCulture);
                var isExpense = transaction.DebtorAccount?.Iban is { } debtorIban
                    ? debtorIban == account.Iban
                    : transaction.CreditDebitIndicator == "DBIT";
                if (isExpense)
                    amount = -amount;

                _db.BankAccountTransactions.Add(new BankAccountTransaction
                {
                    Id = Guid.NewGuid(),
                    LinkedBankAccountId = linkedAccountId,
                    Description = transaction.RemittanceInformation is { Count: > 0 }
                        ? string.Join(" ", transaction.RemittanceInformation)
                        : null,
                    Amount = amount,
                    Currency = transaction.TransactionAmount.Currency,
                    TransactionDate = bookingDate,
                    ExternalTransactionId = externalId
                });

                if (externalId is not null)
                    existingIdSet.Add(externalId);

                added++;
            }

            account.LastSyncedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { added, lastSyncedAt = account.LastSyncedAt });
        }

        /// <summary>
        /// Categorizes every still-uncategorized transaction through the configured categorization service.
        /// The service may use AI, rules, or no external provider at all.
        /// </summary>
        [HttpPost("transactions/{linkedAccountId}/categorize")]
        [Authorize]
        public async Task<IActionResult> CategorizeTransactions(Guid linkedAccountId)
        {
            var userId = _userManager.GetUserId(User)!;
            var account = await _db.LinkedBankAccounts
                .FirstOrDefaultAsync(a => a.Id == linkedAccountId && a.UserId == userId);

            if (account is null)
                return NotFound();

            var uncategorized = await _db.BankAccountTransactions
                .Where(t => t.LinkedBankAccountId == linkedAccountId && t.TransactionCategoryId == null)
                .ToListAsync();

            if (uncategorized.Count == 0)
                return Ok(new { categorized = 0, total = 0 });

            var categories = await _db.TransactionCategories.ToListAsync();
            var categoryIdByName = categories.ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);
            var categoryNames = categories.Select(c => c.Name).ToList();

            var categorized = 0;
            var knownCategoriesByDescription = (await _db.BankAccountTransactions
                .Where(t => t.LinkedBankAccountId == linkedAccountId
                    && t.TransactionCategoryId != null
                    && t.Description != null)
                .Select(t => new { t.Description, t.TransactionCategoryId })
                .ToListAsync())
                .GroupBy(t => NormalizeDescription(t.Description!), StringComparer.Ordinal)
                .Where(group => group.Select(t => t.TransactionCategoryId).Distinct().Count() == 1)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().TransactionCategoryId!.Value,
                    StringComparer.Ordinal);

            var unresolved = new List<BankAccountTransaction>();
            foreach (var transaction in uncategorized)
            {
                if (string.IsNullOrWhiteSpace(transaction.Description))
                    continue;

                if (knownCategoriesByDescription.TryGetValue(NormalizeDescription(transaction.Description), out var categoryId))
                {
                    transaction.TransactionCategoryId = categoryId;
                    categorized++;
                }
                else
                {
                    unresolved.Add(transaction);
                }
            }

            // One representative per merchant description produces the same deterministic category while
            // avoiding repeated prompt and response tokens for duplicate bank entries.
            var unresolvedGroups = unresolved
                .GroupBy(t => NormalizeDescription(t.Description!), StringComparer.Ordinal)
                .ToList();

            foreach (var batch in unresolvedGroups.Chunk(60))
            {
                var payload = batch.Select(group =>
                {
                    var transaction = group.First();
                    return new CategorizationInput(transaction.Id, transaction.Description!, transaction.Amount);
                }).ToList();

                IReadOnlyList<CategorizationResult> result;
                try
                {
                    result = await _categorizationService.CategorizeAsync(
                        payload,
                        categoryNames,
                        HttpContext.RequestAborted);
                }
                catch (HttpRequestException)
                {
                    // A remote categorizer failed for this batch; preserve uncategorized transactions.
                    continue;
                }

                var batchById = batch.ToDictionary(group => group.First().Id);

                foreach (var item in result)
                {
                    // Ignore ids not present in this batch and categories outside the known set.
                    if (item.Category is null)
                        continue;
                    if (!batchById.TryGetValue(item.Id, out var transactions))
                        continue;
                    if (!categoryIdByName.TryGetValue(item.Category, out var categoryId))
                        continue;

                    foreach (var transaction in transactions)
                    {
                        transaction.TransactionCategoryId = categoryId;
                        categorized++;
                    }
                }
            }

            await _db.SaveChangesAsync();

            return Ok(new { categorized, total = uncategorized.Count });
        }

        public record ManualCategoryAssignment(Guid TransactionId, Guid CategoryId);

        /// <summary>
        /// Applies user-picked (not AI-picked) categories to a batch of transactions in one go - the
        /// "select a category, click transactions, save changes" flow on the transactions list page.
        /// Overwrites whatever category (AI-assigned or manual) a transaction already had.
        /// </summary>
        [HttpPatch("transactions/{linkedAccountId}/categories")]
        [Authorize]
        public async Task<IActionResult> SetTransactionCategories(Guid linkedAccountId, [FromBody] List<ManualCategoryAssignment> assignments)
        {
            var userId = _userManager.GetUserId(User)!;
            var account = await _db.LinkedBankAccounts
                .FirstOrDefaultAsync(a => a.Id == linkedAccountId && a.UserId == userId);

            if (account is null)
                return NotFound();

            if (assignments is null || assignments.Count == 0)
                return Ok(new { updated = 0 });

            var validCategoryIds = (await _db.TransactionCategories.Select(c => c.Id).ToListAsync()).ToHashSet();

            var transactionIds = assignments.Select(a => a.TransactionId).ToHashSet();
            var transactions = await _db.BankAccountTransactions
                .Where(t => t.LinkedBankAccountId == linkedAccountId && transactionIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id);

            var updated = 0;
            foreach (var assignment in assignments)
            {
                // Ignore anything pointing at a transaction that isn't actually this account's, or a
                // category id that doesn't exist - never trust the client payload blindly.
                if (!validCategoryIds.Contains(assignment.CategoryId))
                    continue;
                if (!transactions.TryGetValue(assignment.TransactionId, out var transaction))
                    continue;

                transaction.TransactionCategoryId = assignment.CategoryId;
                updated++;
            }

            await _db.SaveChangesAsync();

            return Ok(new { updated });
        }

        [HttpGet("banks")]
        [Authorize]
        public async Task<IActionResult> GetBanks(string country)
        {
            var result = await _client.GetAspspsAsync(country);
            return Ok(result.Aspsps.Select(a => new
            {
                a.Name,
                a.Country,
                a.Bic,
                a.Logo,
                IconPath = ResolveIconPath(a.Name)
            }));
        }

        /// <summary>Resolves a curated bank icon, falling back to the generic icon if the file hasn't been added to wwwroot yet.</summary>
        private string ResolveIconPath(string aspspName)
        {
            var icon = BankIcons.GetByName(aspspName);
            var physicalPath = Path.Combine(_env.WebRootPath, icon.IconPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            return System.IO.File.Exists(physicalPath) ? icon.IconPath : BankIcons.Generic.IconPath;
        }

        private static string NormalizeDescription(string description) =>
            string.Join(' ', description.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .ToUpperInvariant();
    }
}
