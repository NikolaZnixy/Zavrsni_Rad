using Data.Model;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Data.Services
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public DbSet<LinkedBankAccount> LinkedBankAccounts { get; set; } = null!;
        public DbSet<BankAccountTransaction> BankAccountTransactions { get; set; } = null!;
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
        }
    }
}
