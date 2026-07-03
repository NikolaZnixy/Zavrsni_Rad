using Data.Services;
using Microsoft.EntityFrameworkCore;

namespace Web
{
    public static class ProgramExtensions
    {
        public static IServiceCollection AddCoreServices(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("SqliteConnection");
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
            return services;
        }
    }
}
