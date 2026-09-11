using Data.Model;
using Data.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static System.Formats.Asn1.AsnWriter;

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

        public static  IServiceCollection AddIdentity(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDefaultIdentity<AppUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
            })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>();

            //Add google 0Auth
            services
                .AddAuthentication()
                .AddGoogle(options =>
                {
                    options.ClientId = configuration["0Auth:ClientId"]!;
                    options.ClientSecret = configuration["0Auth:ClientSecret"]!;
                });

            return services;
        }

        public static IServiceCollection AddEnableBanking(this IServiceCollection services)
        {
            services.AddHttpClient<EnableBankingClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.enablebanking.com/");
            });

            return services;
        }

        public static IServiceCollection AddCategorization(this IServiceCollection services, IConfiguration configuration)
        {
            var apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                services.AddTransient<Data.Model.Interfaces.ICategorizationService, NoOpCategorizationService>();
                return services;
            }

            services.AddHttpClient<OpenAiCategorization>(client =>
            {
                client.BaseAddress = new Uri("https://api.openai.com/v1/");
            });
            services.AddTransient<Data.Model.Interfaces.IAiCategorization>(
                provider => provider.GetRequiredService<OpenAiCategorization>());
            services.AddTransient<Data.Model.Interfaces.ICategorizationService>(
                provider => provider.GetRequiredService<Data.Model.Interfaces.IAiCategorization>());

            return services;
        }

    }
}
