using Data.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Net;
using Web.Controllers.Api;
using Web.NegativeTests.TestInfrastructure;

namespace Web.NegativeTests
{
    /// <summary>
    /// Covers BankController's negative paths: the two external services it depends on being
    /// unreachable/rejecting credentials, an attacker reusing another user's account id, and a
    /// client submitting an empty/invalid payload.
    /// </summary>
    public class BankControllerTests
    {
        private static BankController BuildController(Data.Services.AppDbContext db, Data.Services.EnableBankingClient bank, Data.Services.GroqClient groq, string callingUserId)
        {
            var env = new Mock<IWebHostEnvironment>();
            env.SetupGet(e => e.WebRootPath).Returns(AppContext.BaseDirectory);

            var controller = new BankController(bank, groq, db, MockUserManager.Create(), env.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                    {
                        User = MockUserManager.PrincipalFor(callingUserId)
                    }
                }
            };
            return controller;
        }

        private static LinkedBankAccount SeedAccount(TestDb db, string ownerUserId)
        {
            // LinkedBankAccount.UserId is a real FK to AspNetUsers, so the owning user has to exist first.
            if (db.Context.Users.Find(ownerUserId) is null)
                db.Context.Users.Add(new AppUser { Id = ownerUserId, UserName = ownerUserId, NormalizedUserName = ownerUserId.ToUpperInvariant() });

            var account = new LinkedBankAccount
            {
                Id = Guid.NewGuid(),
                UserId = ownerUserId,
                DisplayName = "Test account",
                AspspName = "Test Bank",
                Country = "HR",
                EnableBankingAccountId = Guid.NewGuid(),
                Iban = "HR1210010051863000160",
                ConsentValidUntil = DateTimeOffset.UtcNow.AddDays(90),
                LinkedAt = DateTimeOffset.UtcNow
            };
            db.Context.LinkedBankAccounts.Add(account);
            db.Context.SaveChanges();
            return account;
        }

        // --- Enable Banking unavailable / invalid credentials ---------------------------------

        [Fact]
        public async Task SyncTransactions_EnableBankingUnreachable_Returns502_NotUnhandledException()
        {
            using var db = new TestDb();
            var account = SeedAccount(db, "owner-user-id");

            var bankClient = FakeExternalServices.BuildEnableBankingClient(FakeHttpMessageHandler.ThrowingConnectionFailure());
            var groqClient = FakeExternalServices.BuildGroqClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));

            var controller = BuildController(db.Context, bankClient, groqClient, "owner-user-id");

            var result = await controller.SyncTransactions(account.Id);

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, status.StatusCode);
        }

        [Fact]
        public async Task SyncTransactions_EnableBankingRejectsCredentials_Returns502_NotUnhandledException()
        {
            // Simulates an invalid/revoked API credential: Enable Banking answers every request with 401.
            using var db = new TestDb();
            var account = SeedAccount(db, "owner-user-id");

            var bankClient = FakeExternalServices.BuildEnableBankingClient(
                FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_credentials\"}"));
            var groqClient = FakeExternalServices.BuildGroqClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));

            var controller = BuildController(db.Context, bankClient, groqClient, "owner-user-id");

            var result = await controller.SyncTransactions(account.Id);

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, status.StatusCode);
        }

        [Fact]
        public async Task SyncTransactions_AccountBelongsToAnotherUser_ReturnsNotFound()
        {
            using var db = new TestDb();
            var victimAccount = SeedAccount(db, "victim-user-id");

            var bankClient = FakeExternalServices.BuildEnableBankingClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var groqClient = FakeExternalServices.BuildGroqClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var controller = BuildController(db.Context, bankClient, groqClient, "attacker-user-id");

            var result = await controller.SyncTransactions(victimAccount.Id);

            Assert.IsType<NotFoundResult>(result);
        }

        // --- Groq unavailable ------------------------------------------------------------------

        [Fact]
        public async Task CategorizeTransactions_GroqUnreachable_LeavesTransactionsUncategorized_DoesNotThrow()
        {
            using var db = new TestDb();
            var account = SeedAccount(db, "owner-user-id");
            db.Context.BankAccountTransactions.Add(new BankAccountTransaction
            {
                Id = Guid.NewGuid(),
                LinkedBankAccountId = account.Id,
                Description = "Coffee shop",
                Amount = -3.5m,
                Currency = "EUR",
                TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });
            db.Context.SaveChanges();

            var bankClient = FakeExternalServices.BuildEnableBankingClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var groqClient = FakeExternalServices.BuildGroqClient(FakeHttpMessageHandler.ThrowingConnectionFailure());
            var controller = BuildController(db.Context, bankClient, groqClient, "owner-user-id");

            var result = await controller.CategorizeTransactions(account.Id);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
            Assert.Null(db.Context.BankAccountTransactions.Single().TransactionCategoryId);
        }

        [Fact]
        public async Task CategorizeTransactions_GroqReturnsMalformedJson_LeavesTransactionsUncategorized_DoesNotThrow()
        {
            using var db = new TestDb();
            var account = SeedAccount(db, "owner-user-id");
            db.Context.BankAccountTransactions.Add(new BankAccountTransaction
            {
                Id = Guid.NewGuid(),
                LinkedBankAccountId = account.Id,
                Description = "Coffee shop",
                Amount = -3.5m,
                Currency = "EUR",
                TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });
            db.Context.SaveChanges();

            // Groq responds 200 OK but with a completion whose content isn't the expected JSON shape at all.
            const string chatCompletionWithGarbageContent = """
                { "choices": [ { "message": { "role": "assistant", "content": "not json at all" } } ] }
                """;

            var bankClient = FakeExternalServices.BuildEnableBankingClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var groqClient = FakeExternalServices.BuildGroqClient(FakeHttpMessageHandler.ReturningJson(chatCompletionWithGarbageContent));
            var controller = BuildController(db.Context, bankClient, groqClient, "owner-user-id");

            var result = await controller.CategorizeTransactions(account.Id);

            Assert.IsType<OkObjectResult>(result);
            Assert.Null(db.Context.BankAccountTransactions.Single().TransactionCategoryId);
        }

        [Fact]
        public async Task CategorizeTransactions_GroqInventsUnknownCategory_IsIgnored()
        {
            using var db = new TestDb();
            var account = SeedAccount(db, "owner-user-id");
            var transaction = new BankAccountTransaction
            {
                Id = Guid.NewGuid(),
                LinkedBankAccountId = account.Id,
                Description = "Mystery purchase",
                Amount = -3.5m,
                Currency = "EUR",
                TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };
            db.Context.BankAccountTransactions.Add(transaction);
            db.Context.SaveChanges();

            // Groq "hallucinates" a category name that isn't part of the real, closed category set.
            var chatCompletionWithUnknownCategory = $$"""
                { "choices": [ { "message": { "role": "assistant", "content": "{\"categorizations\":[{\"id\":\"{{transaction.Id}}\",\"category\":\"not-a-real-category\"}]}" } } ] }
                """;

            var bankClient = FakeExternalServices.BuildEnableBankingClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var groqClient = FakeExternalServices.BuildGroqClient(FakeHttpMessageHandler.ReturningJson(chatCompletionWithUnknownCategory));
            var controller = BuildController(db.Context, bankClient, groqClient, "owner-user-id");

            var result = await controller.CategorizeTransactions(account.Id);

            Assert.IsType<OkObjectResult>(result);
            Assert.Null(db.Context.BankAccountTransactions.Single().TransactionCategoryId);
        }

        [Fact]
        public async Task CategorizeTransactions_AccountBelongsToAnotherUser_ReturnsNotFound()
        {
            using var db = new TestDb();
            var victimAccount = SeedAccount(db, "victim-user-id");

            var bankClient = FakeExternalServices.BuildEnableBankingClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var groqClient = FakeExternalServices.BuildGroqClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var controller = BuildController(db.Context, bankClient, groqClient, "attacker-user-id");

            var result = await controller.CategorizeTransactions(victimAccount.Id);

            Assert.IsType<NotFoundResult>(result);
        }

        // --- Empty / invalid form submission -----------------------------------------------------

        [Fact]
        public async Task SetTransactionCategories_EmptyList_UpdatesNothing()
        {
            using var db = new TestDb();
            var account = SeedAccount(db, "owner-user-id");

            var bankClient = FakeExternalServices.BuildEnableBankingClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var groqClient = FakeExternalServices.BuildGroqClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var controller = BuildController(db.Context, bankClient, groqClient, "owner-user-id");

            var result = await controller.SetTransactionCategories(account.Id, new List<BankController.ManualCategoryAssignment>());

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public async Task SetTransactionCategories_NullBody_DoesNotThrow()
        {
            using var db = new TestDb();
            var account = SeedAccount(db, "owner-user-id");

            var bankClient = FakeExternalServices.BuildEnableBankingClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var groqClient = FakeExternalServices.BuildGroqClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var controller = BuildController(db.Context, bankClient, groqClient, "owner-user-id");

            var result = await controller.SetTransactionCategories(account.Id, null!);

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task SetTransactionCategories_UnknownCategoryId_IsIgnored_NotApplied()
        {
            using var db = new TestDb();
            var account = SeedAccount(db, "owner-user-id");
            var transaction = new BankAccountTransaction
            {
                Id = Guid.NewGuid(),
                LinkedBankAccountId = account.Id,
                Amount = -10,
                Currency = "EUR",
                TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };
            db.Context.BankAccountTransactions.Add(transaction);
            db.Context.SaveChanges();

            var bankClient = FakeExternalServices.BuildEnableBankingClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var groqClient = FakeExternalServices.BuildGroqClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var controller = BuildController(db.Context, bankClient, groqClient, "owner-user-id");

            var bogusCategoryId = Guid.NewGuid(); // does not exist in TransactionCategories
            var result = await controller.SetTransactionCategories(account.Id,
                new List<BankController.ManualCategoryAssignment> { new(transaction.Id, bogusCategoryId) });

            Assert.IsType<OkObjectResult>(result);
            Assert.Null(db.Context.BankAccountTransactions.Single().TransactionCategoryId);
        }

        [Fact]
        public async Task SetTransactionCategories_TransactionBelongsToAnotherAccount_IsIgnored_NotApplied()
        {
            // A transaction id that's valid, but for a *different* account than the one in the route -
            // guards against a client trying to reassign someone else's transaction through this account's endpoint.
            using var db = new TestDb();
            var ownAccount = SeedAccount(db, "owner-user-id");
            var otherAccount = SeedAccount(db, "someone-else");
            var foreignTransaction = new BankAccountTransaction
            {
                Id = Guid.NewGuid(),
                LinkedBankAccountId = otherAccount.Id,
                Amount = -10,
                Currency = "EUR",
                TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };
            db.Context.BankAccountTransactions.Add(foreignTransaction);
            db.Context.SaveChanges();

            var categoryId = db.Context.TransactionCategories.First().Id;

            var bankClient = FakeExternalServices.BuildEnableBankingClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var groqClient = FakeExternalServices.BuildGroqClient(FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK));
            var controller = BuildController(db.Context, bankClient, groqClient, "owner-user-id");

            var result = await controller.SetTransactionCategories(ownAccount.Id,
                new List<BankController.ManualCategoryAssignment> { new(foreignTransaction.Id, categoryId) });

            Assert.IsType<OkObjectResult>(result);
            Assert.Null(db.Context.BankAccountTransactions.Single(t => t.Id == foreignTransaction.Id).TransactionCategoryId);
        }
    }
}
