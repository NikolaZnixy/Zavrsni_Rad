using Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Web.NegativeTests.TestInfrastructure
{
    /// <summary>
    /// Gives each test its own isolated database: a SQLite connection that only exists in memory
    /// and is thrown away as soon as the test disposes it. Using SQLite (rather than EF Core's
    /// InMemory provider) matters here because a couple of the controllers under test rely on
    /// real SQL behaviour - ExecuteDeleteAsync and the unique filtered index on
    /// (LinkedBankAccountId, ExternalTransactionId) - that the InMemory provider does not support.
    ///
    /// The connection is opened once and kept open for the object's lifetime: SQLite's ":memory:"
    /// database only lives as long as at least one connection to it is open, so closing it early
    /// would wipe the schema between arrange and act.
    /// </summary>
    public sealed class TestDb : IDisposable
    {
        private readonly SqliteConnection _connection;
        public AppDbContext Context { get; }

        public TestDb()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            Context = new AppDbContext(options);
            Context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }
    }
}
