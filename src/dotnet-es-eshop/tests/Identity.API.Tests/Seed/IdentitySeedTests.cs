using LawnDart;
using LawnDart.Aggregates;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Identity.API.Seed;
using LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;
using LawnDart.Samples.EShop.Identity.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Identity.API.Slices.UpdateShopperProfile;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Identity.API.Tests.Seed;

public class IdentitySeedTests
{
    [Fact]
    public async Task Empty_store_seeds_alice_and_bob()
    {
        await using var host = await IdentitySeedHost.StartAsync(enabled: true);
        Assert.Equal(0, await host.CountSeedEventsAsync());

        await host.SeedAsync();

        Assert.Equal(2, IdentitySeedData.Users.Count);
        Assert.Equal(6, await host.CountSeedEventsAsync());
        foreach (var user in IdentitySeedData.Users)
            await host.AssertUserAsync(user);

        var after = await host.CountSeedEventsAsync();
        await host.SeedAsync();
        Assert.Equal(after, await host.CountSeedEventsAsync());
    }

    [Fact]
    public async Task Partial_store_finishes_the_rest_without_duplicates()
    {
        await using var host = await IdentitySeedHost.StartAsync(enabled: true);
        var alice = IdentitySeedData.Users[0];
        await host.Register.HandleAsync(alice.RegisterCommand());
        Assert.Equal(1, await host.StreamLengthAsync($"ShopperAccount:{alice.AccountId}"));

        await host.SeedAsync();

        var aliceEvents = await host.ReadAsync($"ShopperAccount:{alice.AccountId}");
        Assert.Single(aliceEvents.OfType<ShopperAccountRegistered>());
        Assert.Single(aliceEvents.OfType<ShopperProfileUpdated>());
        Assert.Equal(6, await host.CountSeedEventsAsync());
        foreach (var user in IdentitySeedData.Users)
            await host.AssertUserAsync(user);
    }

    [Fact]
    public async Task User_name_owned_by_another_account_is_skipped()
    {
        await using var host = await IdentitySeedHost.StartAsync(enabled: true);
        var alice = IdentitySeedData.Users[0];
        var otherId = Guid.NewGuid();
        await host.Register.HandleAsync(new RegisterShopperAccountCommand(
            Guid.NewGuid(),
            otherId,
            alice.UserName,
            "other-alice@email.com",
            alice.Password,
            "Other",
            "Person"));

        await host.SeedAsync();

        var login = await host.Users.FindByNameAsync(alice.UserName);
        Assert.NotNull(login);
        Assert.Equal(otherId.ToString(), login.Id);
        Assert.Equal("other-alice@email.com", login.Email);
        Assert.False(login.EmailConfirmed);
        Assert.Null(login.CardNumber);
        Assert.Null(login.SecurityNumber);
        Assert.Equal(0, await host.StreamLengthAsync($"ShopperAccount:{alice.AccountId}"));
        Assert.Equal(1, await host.StreamLengthAsync($"ShopperAccount:{otherId}"));
        await host.AssertUserAsync(IdentitySeedData.Users[1]);
        Assert.Contains(host.Logs, line => line.Contains("another account", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Disabled_seed_writes_nothing()
    {
        await using var host = await IdentitySeedHost.StartAsync(enabled: false);

        await host.SeedAsync();

        Assert.Equal(0, await host.CountSeedEventsAsync());
        Assert.Null(await host.Users.FindByNameAsync("alice"));
        Assert.Contains(host.Logs, line => line.Contains("disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Existing_login_tables_gain_card_columns_before_seed()
    {
        await using var host = await IdentitySeedHost.StartAsync(enabled: true, legacyLoginSchema: true);
        var before = await host.UserColumnNamesAsync();
        Assert.DoesNotContain("CardNumber", before);
        Assert.DoesNotContain("CardHolderName", before);
        Assert.DoesNotContain("CardType", before);
        Assert.DoesNotContain("Expiration", before);
        Assert.DoesNotContain("SecurityNumber", before);

        await host.InitializeLoginAsync();

        var added = await host.UserColumnNamesAsync();
        Assert.Contains("CardNumber", added);
        Assert.Contains("CardHolderName", added);
        Assert.Contains("CardType", added);
        Assert.Contains("Expiration", added);
        Assert.Contains("SecurityNumber", added);

        await host.SeedAsync();
        foreach (var user in IdentitySeedData.Users)
            await host.AssertUserAsync(user);

        var afterSeed = await host.UserColumnNamesAsync();
        var aliceCard = (await host.Users.FindByNameAsync("alice"))!.CardNumber;

        await host.InitializeLoginAsync();

        Assert.Equal(afterSeed, await host.UserColumnNamesAsync());
        Assert.Equal(aliceCard, (await host.Users.FindByNameAsync("alice"))!.CardNumber);
        foreach (var user in IdentitySeedData.Users)
            await host.AssertUserAsync(user);
    }

    private sealed class IdentitySeedHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly AsyncServiceScope _scope;
        private readonly SqliteConnection _connection;
        private readonly List<string> _logs;

        private IdentitySeedHost(
            WebApplication app,
            AsyncServiceScope scope,
            SqliteConnection connection,
            List<string> logs,
            IdentitySeeder seeder,
            IEventStore store,
            IAggregateRepository repository,
            UserManager<ApplicationUser> users,
            ICommandHandler<RegisterShopperAccountCommand> register)
        {
            _app = app;
            _scope = scope;
            _connection = connection;
            _logs = logs;
            Seeder = seeder;
            Store = store;
            Repository = repository;
            Users = users;
            Register = register;
        }

        public IdentitySeeder Seeder { get; }

        public IEventStore Store { get; }

        public IAggregateRepository Repository { get; }

        public UserManager<ApplicationUser> Users { get; }

        public ICommandHandler<RegisterShopperAccountCommand> Register { get; }

        public IReadOnlyList<string> Logs => _logs;

        public Task SeedAsync() => Seeder.SeedAsync();

        public Task InitializeLoginAsync() => _app.InitializeShopperLoginStoreAsync();

        public async Task<IReadOnlyList<string>> UserColumnNamesAsync()
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT name FROM pragma_table_info('AspNetUsers') ORDER BY name";
            var names = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                names.Add(reader.GetString(0));

            return names;
        }

        public static Task<IdentitySeedHost> StartAsync(bool enabled) =>
            StartAsync(enabled, legacyLoginSchema: false);

        public static async Task<IdentitySeedHost> StartAsync(bool enabled, bool legacyLoginSchema)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            if (legacyLoginSchema)
            {
                var legacyOptions = new DbContextOptionsBuilder<LegacyShopperLoginDbContext>()
                    .UseSqlite(connection)
                    .Options;
                await using var legacy = new LegacyShopperLoginDbContext(legacyOptions);
                await legacy.Database.EnsureCreatedAsync();
            }
            var logs = new List<string>();
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(new ListLoggerProvider(logs));
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EShop:Seed:Enabled"] = enabled ? "true" : "false"
            });
            builder.Services.AddLogging();
            builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            builder.Services
                .AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            builder.Services.AddScoped<IShopperLoginStore, ShopperLoginStore>();
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            context.AddIdentityContext(typeof(ShopperAccount).Assembly);
            builder.AddIdentitySeed();

            var app = builder.Build();
            var scope = app.Services.CreateAsyncScope();
            if (!legacyLoginSchema)
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Database.EnsureCreatedAsync();
            }

            var provider = scope.ServiceProvider;
            return new IdentitySeedHost(
                app,
                scope,
                connection,
                logs,
                provider.GetRequiredService<IdentitySeeder>(),
                provider.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName),
                provider.GetRequiredService<IAggregateRepository>(),
                provider.GetRequiredService<UserManager<ApplicationUser>>(),
                provider.GetRequiredService<ICommandHandler<RegisterShopperAccountCommand>>());
        }

        public async Task AssertUserAsync(IdentitySeedUser user)
        {
            var login = await Users.FindByNameAsync(user.UserName);
            Assert.NotNull(login);
            Assert.Equal(user.AccountId.ToString(), login.Id);
            Assert.Equal(user.Email, login.Email);
            Assert.True(login.EmailConfirmed);
            Assert.True(await Users.CheckPasswordAsync(login, user.Password));

            var account = await Repository.GetOrCreateAsync<ShopperAccount>(user.AccountId);
            Assert.Equal(user.Name, account.State.Name);
            Assert.Equal(user.LastName, account.State.LastName);
            Assert.Equal(user.PhoneNumber, account.State.PhoneNumber);
            Assert.Equal(user.Street, account.State.Street);
            Assert.Equal(user.City, account.State.City);
            Assert.Equal(user.State, account.State.State);
            Assert.Equal(user.Country, account.State.Country);
            Assert.Equal(user.ZipCode, account.State.ZipCode);
            Assert.Equal(user.CardHolderName, login.CardHolderName);
            Assert.Equal(user.CardNumber, login.CardNumber);
            Assert.Equal(user.CardType, login.CardType);
            Assert.Equal(user.Expiration, login.Expiration);
            Assert.Equal(user.SecurityNumber, login.SecurityNumber);
        }

        public async Task<int> CountSeedEventsAsync()
        {
            var total = 0;
            foreach (var user in IdentitySeedData.Users)
            {
                total += await StreamLengthAsync($"ShopperAccount:{user.AccountId}");
                total += await StreamLengthAsync(ShopperEmail.EmailStreamId(user.Email));
            }

            return total;
        }

        public async Task<int> StreamLengthAsync(string streamId) => (await ReadAsync(streamId)).Count;

        public async Task<IReadOnlyList<IEvent>> ReadAsync(string streamId)
        {
            var recorded = await Store.ReadStreamAsync(streamId);
            return recorded.Select(row => row.Event).ToList();
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _app.DisposeAsync();
            await _connection.DisposeAsync();
        }
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

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Information)
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

    /// <summary>
    /// Login tables from before the card columns existed.
    /// </summary>
    private sealed class LegacyShopperLoginDbContext : IdentityDbContext<ApplicationUser>
    {
        public LegacyShopperLoginDbContext(DbContextOptions<LegacyShopperLoginDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<ApplicationUser>().Ignore(user => user.CardHolderName);
            builder.Entity<ApplicationUser>().Ignore(user => user.CardNumber);
            builder.Entity<ApplicationUser>().Ignore(user => user.CardType);
            builder.Entity<ApplicationUser>().Ignore(user => user.Expiration);
            builder.Entity<ApplicationUser>().Ignore(user => user.SecurityNumber);
        }
    }
}
