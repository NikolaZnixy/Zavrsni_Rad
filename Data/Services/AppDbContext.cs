using Data.Model;
using Data.Model.Data;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Data.Services
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public DbSet<LinkedBankAccount> LinkedBankAccounts { get; set; } = null!;
        public DbSet<BankAccountTransaction> BankAccountTransactions { get; set; } = null!;
        public DbSet<TransactionCategory> TransactionCategories { get; set; } = null!;
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BankAccountTransaction>()
                .HasIndex(t => new { t.LinkedBankAccountId, t.ExternalTransactionId })
                .IsUnique()
                .HasFilter("\"ExternalTransactionId\" IS NOT NULL");

            modelBuilder.Entity<BankAccountTransaction>()
                .HasOne(t => t.TransactionCategory)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.TransactionCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<TransactionCategory>().HasData(
                new TransactionCategory { Id = TransactionCategorySeedIds.Car, Name = "car" },
                new TransactionCategory { Id = TransactionCategorySeedIds.Gift, Name = "gift" },
                new TransactionCategory { Id = TransactionCategorySeedIds.Luxury, Name = "luxury" },
                new TransactionCategory { Id = TransactionCategorySeedIds.Groceries, Name = "groceries" },
                new TransactionCategory { Id = TransactionCategorySeedIds.Subscriptions, Name = "subscriptions" }
            );
        }
    }
}
