using Data.Model;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Data.Services
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public DbSet<LinkedBankAccount> LinkedBankAccounts { get; set; } = null!;
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
    }
}
