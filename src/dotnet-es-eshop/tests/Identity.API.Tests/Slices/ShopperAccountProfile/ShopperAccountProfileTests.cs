using System.Net;
using System.Text.Json;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Slices.ShopperAccountProfile;
using LawnDart.Samples.EShop.Identity.API.Slices.UpdateShopperProfile;
using LawnDart.Samples.EShop.Identity.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Testing.Bdd;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Identity.API.Tests.Slices.ShopperAccountProfile;

public class ShopperAccountProfileTests
{
    private const string Password = "Pass123$";

    [Fact]
    public async Task Registration_creates_the_profile()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(ShopperAccountRegistered),
            typeof(ShopperProfileUpdated));
        var accountId = Guid.NewGuid();
        var projection = new ShopperAccountProfileProjection();

        await AggregateSpec
            .For<ShopperAccount>(ctx, accountId)
            .When(new RegisterShopperAccountCommand(
                Guid.NewGuid(),
                accountId,
                "ada",
                "ada@example.com",
                Password,
                "Ada",
                "Lovelace"))
            .AndView(runner => runner.Register(@event => projection.ProcessEvent(@event, 0)))
            .AndAssert(_ =>
            {
                var view = projection.GetView();
                Assert.Equal(accountId, view.AccountId);
                Assert.Equal("ada", view.UserName);
                Assert.Equal("ada@example.com", view.Email);
                Assert.Equal("Ada", view.Name);
                Assert.Equal("Lovelace", view.LastName);
                Assert.Null(view.Street);
                Assert.Null(view.City);
                Assert.Null(view.State);
                Assert.Null(view.Country);
                Assert.Null(view.ZipCode);
                Assert.Null(view.PhoneNumber);
                Assert.True(view.IsActive);
                AssertSecretAbsent(view, Password);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Profile_update_changes_the_profile()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(ShopperAccountRegistered),
            typeof(ShopperProfileUpdated));
        var accountId = Guid.NewGuid();
        var projection = new ShopperAccountProfileProjection();

        await AggregateSpec
            .For<ShopperAccount>(ctx, accountId)
            .Given(Registered(accountId))
            .When(new UpdateShopperProfileCommand(
                Guid.NewGuid(),
                accountId,
                "Augusta",
                "King",
                "555-0100",
                "1 Main",
                "Redmond",
                "WA",
                "USA",
                "98052"))
            .AndView(runner => runner.Register(@event => projection.ProcessEvent(@event, 0)))
            .AndAssert(_ =>
            {
                var view = projection.GetView();
                Assert.Equal(accountId, view.AccountId);
                Assert.Equal("ada", view.UserName);
                Assert.Equal("ada@example.com", view.Email);
                Assert.Equal("Augusta", view.Name);
                Assert.Equal("King", view.LastName);
                Assert.Equal("555-0100", view.PhoneNumber);
                Assert.Equal("1 Main", view.Street);
                Assert.Equal("Redmond", view.City);
                Assert.Equal("WA", view.State);
                Assert.Equal("USA", view.Country);
                Assert.Equal("98052", view.ZipCode);
                Assert.True(view.IsActive);
                AssertSecretAbsent(view, Password);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Unknown_account_returns_not_found()
    {
        await using var host = await ProfileHost.StartAsync();

        var missing = await host.Client.GetAsync(ProfileHost.Url(Guid.NewGuid()));
        var empty = await host.Client.GetAsync(ShopperAccountProfileEndpoint.Route);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, empty.StatusCode);
    }

    [Fact]
    public async Task Get_returns_the_profile_for_the_account()
    {
        await using var host = await ProfileHost.StartAsync();
        var accountId = Guid.NewGuid();
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);

        await store.AppendAsync(
            ShopperAccountProfileProjection.InstanceId(accountId),
            [Registered(accountId)]);

        var created = await host.PollAsync(accountId, view => view.Name == "Ada");
        Assert.Equal("ada", created.UserName);
        Assert.Equal("ada@example.com", created.Email);
        Assert.Equal("Lovelace", created.LastName);
        Assert.True(created.IsActive);
        Assert.Null(created.Street);
        AssertSecretAbsent(created, Password);

        await store.AppendAsync(
            ShopperAccountProfileProjection.InstanceId(accountId),
            [new ShopperProfileUpdated(
                Guid.NewGuid(),
                DateTime.UtcNow,
                accountId,
                "Augusta",
                "King",
                "555-0100",
                "1 Main",
                "Redmond",
                "WA",
                "USA",
                "98052")]);

        var updated = await host.PollAsync(accountId, view => view.Name == "Augusta");
        Assert.Equal("ada", updated.UserName);
        Assert.Equal("ada@example.com", updated.Email);
        Assert.Equal("King", updated.LastName);
        Assert.Equal("1 Main", updated.Street);
        Assert.Equal("98052", updated.ZipCode);
        Assert.Equal("555-0100", updated.PhoneNumber);
    }

    private static ShopperAccountRegistered Registered(Guid accountId) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            accountId,
            "ada",
            "ada@example.com",
            "Ada",
            "Lovelace");

    private static void AssertSecretAbsent(ShopperAccountProfileView view, string password)
    {
        var names = typeof(ShopperAccountProfileView).GetProperties().Select(property => property.Name);
        Assert.DoesNotContain(names, name =>
            name.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Hash", StringComparison.OrdinalIgnoreCase));

        var json = JsonSerializer.Serialize(view);
        Assert.DoesNotContain(password, json, StringComparison.Ordinal);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ProfileHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private ProfileHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public IServiceProvider Services => _app.Services;

        public static async Task<ProfileHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            context.AddIdentityContext(typeof(ShopperAccountProfileProjection).Assembly);
            var bearer = new MatchingShopperBearerHandler().Protect(builder.Services);

            var app = builder.Build();
            app.MapShopperAccountProfile();
            await app.StartAsync();

            var address = app.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .Single();
            return new ProfileHost(app, bearer.CreateClient(address));
        }

        public async Task<ShopperAccountProfileView> PollAsync(
            Guid accountId,
            Func<ShopperAccountProfileView, bool> matches)
        {
            string? lastBody = null;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                using var response = await Client.GetAsync(Url(accountId));
                lastBody = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var view = JsonSerializer.Deserialize<ShopperAccountProfileView>(
                        lastBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (view is not null && matches(view))
                    {
                        AssertSecretAbsent(view, Password);
                        return view;
                    }
                }

                await Task.Delay(100);
            }

            Assert.Fail($"Profile was not ready. Last body: {lastBody}");
            throw new InvalidOperationException(lastBody);
        }

        public static string Url(Guid accountId) =>
            $"{ShopperAccountProfileEndpoint.Route}?accountId={accountId:D}";

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
