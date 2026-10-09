using LawnDart.Samples.EShop.Identity.API.Login;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Identity.API.Tests.Slices.RegisterShopperAccount;

/// <summary>
/// SQLite stand-in for the SQL Server login store. Unit tests do not start Docker.
/// </summary>
internal sealed class ShopperLoginTestHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;
    private readonly List<string> _logs;

    private ShopperLoginTestHost(
        SqliteConnection connection,
        ServiceProvider provider,
        AsyncServiceScope scope,
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        IShopperLoginStore store,
        List<string> logs)
    {
        _connection = connection;
        _provider = provider;
        _scope = scope;
        _logs = logs;
        Db = db;
        Users = users;
        Store = store;
    }

    public ApplicationDbContext Db { get; }

    public UserManager<ApplicationUser> Users { get; }

    public IShopperLoginStore Store { get; }

    public IReadOnlyList<string> Logs => _logs;

    public static async Task<ShopperLoginTestHost> StartAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var logs = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(new ListLoggerProvider(logs)));
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services
            .AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<IShopperLoginStore, ShopperLoginStore>();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        return new ShopperLoginTestHost(
            connection,
            provider,
            scope,
            db,
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<IShopperLoginStore>(),
            logs);
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private sealed class ListLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _logs;

        public ListLoggerProvider(List<string> logs) => _logs = logs;

        public ILogger CreateLogger(string categoryName) => new ListLogger(_logs);

        public void Dispose()
        {
        }

        private sealed class ListLogger : ILogger
        {
            private readonly List<string> _logs;

            public ListLogger(List<string> logs) => _logs = logs;

            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull =>
                NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _logs.Add(formatter(state, exception));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
