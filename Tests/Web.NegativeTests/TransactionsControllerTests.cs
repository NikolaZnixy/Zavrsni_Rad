using Data.Model;
using Microsoft.AspNetCore.Mvc;
using Web.Controllers;
using Web.Models;
using Web.NegativeTests.TestInfrastructure;

namespace Web.NegativeTests
{
    /// <summary>
    /// Covers the IDOR (Insecure Direct Object Reference) and unknown/empty-identifier scenarios for
    /// TransactionsController: every action takes a Guid accountId straight from the route, so the
    /// only thing standing between one user and another user's transactions is the ownership check
    /// against UserId inside each action.
    /// </summary>
    public class TransactionsControllerTests
    {
        private static TransactionsController BuildController(TestDb db, string callingUserId)
        {
            var controller = new TransactionsController(db.Context, MockUserManager.Create())
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
                DisplayName = "Victim's account",
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

        [Fact]
        public async Task List_AccountBelongsToAnotherUser_ReturnsNotFound_NotTheirData()
        {
            // Arrange: account and its transactions belong to "victim", attacker requests them by guessing/reusing the guid.
            using var db = new TestDb();
            var victimAccount = SeedAccount(db, ownerUserId: "victim-user-id");
            db.Context.BankAccountTransactions.Add(new BankAccountTransaction
            {
                Id = Guid.NewGuid(),
                LinkedBankAccountId = victimAccount.Id,
                Amount = -50,
                Currency = "EUR",
                TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });
            db.Context.SaveChanges();

            var controller = BuildController(db, callingUserId: "attacker-user-id");

            // Act
            var result = await controller.List(victimAccount.Id);

            // Assert: 404, not 403 - and definitely not the victim's transactions in a 200.
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task List_UnknownAccountId_ReturnsNotFound()
        {
            using var db = new TestDb();
            var controller = BuildController(db, callingUserId: "some-user-id");

            var result = await controller.List(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task List_EmptyGuid_ReturnsNotFound()
        {
            using var db = new TestDb();
            var controller = BuildController(db, callingUserId: "some-user-id");

            var result = await controller.List(Guid.Empty);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task List_OwnAccount_ReturnsOnlyThatAccountsTransactions()
        {
            using var db = new TestDb();
            var ownAccount = SeedAccount(db, ownerUserId: "owner-user-id");
            var otherAccount = SeedAccount(db, ownerUserId: "someone-else");

            db.Context.BankAccountTransactions.AddRange(
                new BankAccountTransaction { Id = Guid.NewGuid(), LinkedBankAccountId = ownAccount.Id, Amount = -10, Currency = "EUR", TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow) },
                new BankAccountTransaction { Id = Guid.NewGuid(), LinkedBankAccountId = otherAccount.Id, Amount = -999, Currency = "EUR", TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow) }
            );
            db.Context.SaveChanges();

            var controller = BuildController(db, callingUserId: "owner-user-id");

            var result = await controller.List(ownAccount.Id);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<TransactionsListViewModel>(view.Model);
            var transaction = Assert.Single(model.Transactions);
            Assert.Equal(-10, transaction.Amount);
        }

        [Fact]
        public async Task Clear_AccountBelongsToAnotherUser_ReturnsNotFound_DoesNotDeleteVictimsData()
        {
            using var db = new TestDb();
            var victimAccount = SeedAccount(db, ownerUserId: "victim-user-id");
            db.Context.BankAccountTransactions.Add(new BankAccountTransaction
            {
                Id = Guid.NewGuid(),
                LinkedBankAccountId = victimAccount.Id,
                Amount = -50,
                Currency = "EUR",
                TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });
            db.Context.SaveChanges();

            var controller = BuildController(db, callingUserId: "attacker-user-id");

            var result = await controller.Clear(victimAccount.Id);

            Assert.IsType<NotFoundResult>(result);
            Assert.Equal(1, db.Context.BankAccountTransactions.Count());
        }

        [Fact]
        public async Task Clear_OwnAccount_DeletesTransactionsAndResetsLastSyncedAt()
        {
            using var db = new TestDb();
            var account = SeedAccount(db, ownerUserId: "owner-user-id");
            account.LastSyncedAt = DateTimeOffset.UtcNow;
            db.Context.BankAccountTransactions.Add(new BankAccountTransaction
            {
                Id = Guid.NewGuid(),
                LinkedBankAccountId = account.Id,
                Amount = -50,
                Currency = "EUR",
                TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });
            db.Context.SaveChanges();

            var controller = BuildController(db, callingUserId: "owner-user-id");

            var result = await controller.Clear(account.Id);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Empty(db.Context.BankAccountTransactions);
            Assert.Null(db.Context.LinkedBankAccounts.Single().LastSyncedAt);
        }
    }
}
