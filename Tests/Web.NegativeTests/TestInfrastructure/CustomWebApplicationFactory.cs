using Data.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Web.NegativeTests.TestInfrastructure
{
    /// <summary>
    /// Boots the real ASP.NET Core pipeline (routing, [Authorize], model binding, everything Program.cs
    /// wires up) against an in-memory SQLite database instead of the real app.db/SQL Server, so the
    /// route-level and HTTP-level negative scenarios (unauthenticated access, malformed route values,
    /// empty form posts) are exercised the same way a real browser would trigger them, without needing
    /// a real database file or any of the external services configured.
    /// </summary>
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");

        public CustomWebApplicationFactory()
        {
            _connection.Open();

            // The schema has to exist *before* the host starts: Program.cs seeds the User/Admin roles
            // during startup (before any test gets a chance to call EnsureDatabaseCreated), so building
            // it here - straight from a throwaway AppDbContext over the same open connection, without
            // going through the app's DI container - avoids a "no such table" failure on first boot.
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            using var db = new AppDbContext(options);
            db.Database.EnsureCreated();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                // Dummy values so EnableBankingClient/GroqClient/Google OAuth construct without throwing
                // on missing configuration - none of the tests that use this factory actually call out
                // to those services.
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["EnableBanking:ApplicationId"] = "test-application-id",
                    ["EnableBanking:PrivateKeyPem"] = FakeExternalServices.GenerateRsaPrivateKeyPem(),
                    ["Groq:ApiKey"] = "test-groq-key",
                    ["0Auth:ClientId"] = "test-client-id",
                    ["0Auth:ClientSecret"] = "test-client-secret",
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

                // Registered alongside the real Identity cookie scheme (which stays the default, so the
                // anonymous-access-redirects-to-login tests keep exercising real behaviour). Individual
                // tests that need a genuinely authenticated request opt into it via WithAuthenticatedClient.
                services.AddAuthentication().AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                _connection.Dispose();
        }

        /// <summary>Schema is already created by the constructor; kept as a harmless idempotent call for test readability.</summary>
        public void EnsureDatabaseCreated()
        {
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        }

        /// <summary>
        /// An HttpClient that is genuinely authenticated (as <paramref name="userId"/>) for every request,
        /// via the "Test" scheme registered above - for tests that need to get past [Authorize] to reach
        /// model binding/action code, without going through a real Identity login flow.
        /// </summary>
        public HttpClient CreateAuthenticatedClient(string userId)
        {
            var authenticatedFactory = WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                    services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })));

            var client = authenticatedFactory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            return client;
        }
    }
}
