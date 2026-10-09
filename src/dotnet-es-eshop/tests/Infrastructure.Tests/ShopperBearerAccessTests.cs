using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LawnDart;
using LawnDart.AspNetCore;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Basket.API.Aggregates.Basket;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Basket.API.Slices.BasketContents;
using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Slices.ShopperAccountProfile;
using LawnDart.Samples.EShop.Ordering.API;
using LawnDart.Samples.EShop.Ordering.API.Slices.OrderSummary;
using LawnDart.Samples.EShop.Ordering.API.Slices.OrdersByShopper;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

public class ShopperBearerAccessTests
{
    private const string Password = "Pass123$";

    [Fact]
    public async Task Basket_requires_a_token_for_the_caller()
    {
        var key = ShopperAccessTokens.CreateKey();
        var other = ShopperAccessTokens.CreateKey();
        await using var host = await ShopperHost.StartBasketAsync(key);
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var aliceToken = ShopperAccessTokens.Issue(key, alice, "Alice").Value;
        var expired = ShopperAccessTokens.Issue(
            key,
            alice,
            "Alice",
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-10),
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-20)).Value;
        var wrongKey = ShopperAccessTokens.Issue(other, alice, "Alice").Value;

        using var missing = await host.PostAsync("/api/basket/add-item-to-basket", AddItem(alice), token: null);
        using var stale = await host.PostAsync("/api/basket/add-item-to-basket", AddItem(alice), expired);
        using var forged = await host.PostAsync("/api/basket/add-item-to-basket", AddItem(alice), wrongKey);
        using var otherBuyer = await host.PostAsync("/api/basket/add-item-to-basket", AddItem(bob), aliceToken);
        using var own = await host.PostAsync("/api/basket/add-item-to-basket", AddItem(alice), aliceToken);
        using var view = await host.GetAsync($"{BasketContentsEndpoint.Route}?buyerId={alice:D}", aliceToken);
        using var viewOther = await host.GetAsync($"{BasketContentsEndpoint.Route}?buyerId={bob:D}", aliceToken);
        using var missingBuyer = await host.GetAsync(BasketContentsEndpoint.Route, aliceToken);

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherBuyer.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, own.StatusCode);
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, viewOther.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingBuyer.StatusCode);
        Assert.DoesNotContain(aliceToken, await otherBuyer.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ordering_requires_a_token_for_the_caller()
    {
        var key = ShopperAccessTokens.CreateKey();
        await using var host = await ShopperHost.StartOrderingAsync(key);
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var aliceToken = ShopperAccessTokens.Issue(key, alice, "Alice").Value;
        var expired = ShopperAccessTokens.Issue(
            key,
            alice,
            "Alice",
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-10),
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-20)).Value;

        using var missing = await host.PostAsync("/api/ordering/place-order", Place(alice), token: null);
        using var stale = await host.PostAsync("/api/ordering/place-order", Place(alice), expired);
        using var otherBuyer = await host.PostAsync("/api/ordering/place-order", Place(bob), aliceToken);
        using var own = await host.PostAsync("/api/ordering/place-order", Place(alice), aliceToken);
        using var list = await host.GetAsync($"{OrdersByShopperEndpoint.Route}?buyerId={alice:D}", aliceToken);
        using var listOther = await host.GetAsync($"{OrdersByShopperEndpoint.Route}?buyerId={bob:D}", aliceToken);
        var wrongKey = ShopperAccessTokens.Issue(ShopperAccessTokens.CreateKey(), alice, "Alice").Value;
        using var forged = await host.PostAsync("/api/ordering/place-order", Place(alice), wrongKey);

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherBuyer.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, own.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, listOther.StatusCode);
    }

    [Fact]
    public async Task Identity_profile_requires_a_token_for_the_caller()
    {
        var key = ShopperAccessTokens.CreateKey();
        var other = ShopperAccessTokens.CreateKey();
        await using var host = await ShopperHost.StartProfileAsync(key);
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var aliceToken = ShopperAccessTokens.Issue(key, alice, "Alice").Value;
        var expired = ShopperAccessTokens.Issue(
            key,
            alice,
            "Alice",
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-10),
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-20)).Value;
        var wrongKey = ShopperAccessTokens.Issue(other, alice, "Alice").Value;
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);
        await store.AppendAsync(
            ShopperAccountProfileProjection.InstanceId(alice),
            [new ShopperAccountRegistered(Guid.NewGuid(), DateTime.UtcNow, alice, "alice", "alice@example.com", "Alice", "Smith")]);

        using var missing = await host.GetAsync(ProfileUrl(alice), token: null);
        using var stale = await host.GetAsync(ProfileUrl(alice), expired);
        using var forged = await host.GetAsync(ProfileUrl(alice), wrongKey);
        using var otherAccount = await host.GetAsync(ProfileUrl(bob), aliceToken);
        var (ownStatus, ownBody) = await host.PollAsync(ProfileUrl(alice), aliceToken, HttpStatusCode.OK);
        using var signOut = await host.PostAsync(
            "/api/identity/sign-out-shopper",
            new { id = Guid.NewGuid(), accountId = bob },
            aliceToken);

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherAccount.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownStatus);
        Assert.Contains("Alice", ownBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, signOut.StatusCode);
    }

    [Fact]
    public async Task Identity_sign_in_token_is_accepted_by_basket()
    {
        await using var identity = await ShopperHost.StartIdentityAsync();
        var accountId = Guid.NewGuid();
        using var register = await identity.PostAsync("/api/identity/register-shopper-account", new
        {
            id = Guid.NewGuid(),
            accountId,
            userName = "alice",
            email = "alice@example.com",
            password = Password,
            name = "Alice",
            lastName = "Smith"
        }, token: null);
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);

        using var signIn = await identity.PostAsync("/api/identity/sign-in-shopper", new
        {
            id = Guid.NewGuid(),
            userName = "alice",
            password = Password,
            rememberLogin = false
        }, token: null);
        var signInJson = await signIn.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Accepted, signIn.StatusCode);
        Assert.DoesNotContain(Password, signInJson, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(signInJson);
        Assert.Equal(accountId, document.RootElement.GetProperty("accountId").GetGuid());
        var token = document.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrEmpty(token));
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);
        Assert.Equal(accountId.ToString("D"), jwt.GetClaim("sub").Value);
        Assert.Equal("Alice Smith", jwt.GetClaim("name").Value);
        Assert.Equal(ShopperAccessTokens.Issuer, jwt.Issuer);
        Assert.Equal(ShopperAccessTokens.Audience, Assert.Single(jwt.Audiences));

        var jwks = await identity.Client.GetStringAsync(ShopperAccessTokens.JwksPath);
        Assert.DoesNotContain("\"d\":", jwks, StringComparison.Ordinal);
        Assert.Contains("\"n\":", jwks, StringComparison.Ordinal);

        await using var basket = await ShopperHost.StartBasketFromIdentityAsync(identity.Address);
        using var own = await basket.PostAsync("/api/basket/add-item-to-basket", AddItem(accountId), token);
        using var otherBuyer = await basket.PostAsync("/api/basket/add-item-to-basket", AddItem(Guid.NewGuid()), token);
        using var missing = await basket.PostAsync("/api/basket/add-item-to-basket", AddItem(accountId), token: null);

        Assert.Equal(HttpStatusCode.Accepted, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherBuyer.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
    }

    [Fact]
    public async Task Public_jwks_omits_the_private_exponent()
    {
        var key = ShopperAccessTokens.CreateKey();
        var json = ShopperAccessTokens.WritePublicJwks(key);
        var token = ShopperAccessTokens.Issue(key, Guid.NewGuid(), "Ada");
        var set = new JsonWebKeySet(json);
        var parameters = ShopperAccessTokens.ValidationParameters();
        parameters.IssuerSigningKeys = set.GetSigningKeys();
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, parameters);

        Assert.DoesNotContain("\"d\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"p\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"q\":", json, StringComparison.Ordinal);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Alice_cannot_change_bobs_basket_or_profile()
    {
        var key = ShopperAccessTokens.CreateKey();
        await using var basket = await ShopperHost.StartBasketAsync(key);
        await using var identity = await ShopperHost.StartProfileAsync(key);
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var token = ShopperAccessTokens.Issue(key, alice, "Alice").Value;
        var productId = Guid.NewGuid();

        using var quantity = await basket.PostAsync(
            "/api/basket/change-basket-item-quantity",
            new { id = Guid.NewGuid(), buyerId = bob, productId, quantity = 2 },
            token);
        using var remove = await basket.PostAsync(
            "/api/basket/remove-item-from-basket",
            new { id = Guid.NewGuid(), buyerId = bob, productId },
            token);
        using var clear = await basket.PostAsync(
            "/api/basket/clear-basket",
            new { id = Guid.NewGuid(), buyerId = bob },
            token);
        using var profile = await identity.PostAsync(
            "/api/identity/update-shopper-profile",
            new { id = Guid.NewGuid(), accountId = bob, name = "Bob", lastName = "Smith" },
            token);

        Assert.Equal(HttpStatusCode.Forbidden, quantity.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, remove.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, clear.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, profile.StatusCode);
    }

    [Fact]
    public async Task Order_summary_and_cancel_stay_with_the_buyer()
    {
        var key = ShopperAccessTokens.CreateKey();
        await using var host = await ShopperHost.StartOrderingAsync(key);
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var aliceToken = ShopperAccessTokens.Issue(key, alice, "Alice").Value;
        var bobToken = ShopperAccessTokens.Issue(key, bob, "Bob").Value;
        var orderId = Guid.NewGuid();

        using var placed = await host.PostAsync("/api/ordering/place-order", Place(bob, orderId), bobToken);
        Assert.Equal(HttpStatusCode.Accepted, placed.StatusCode);

        using var otherBuyer = await host.GetAsync($"/views/order-summary/{orderId:D}?buyerId={bob:D}", aliceToken);
        using var ownBuyer = await host.GetAsync($"/views/order-summary/{orderId:D}?buyerId={alice:D}", aliceToken);
        using var cancel = await host.PostAsync(
            "/api/ordering/cancel-order",
            new { commandId = Guid.NewGuid(), orderId },
            aliceToken);

        Assert.Equal(HttpStatusCode.Forbidden, otherBuyer.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ownBuyer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
    }

    [Fact]
    public async Task Internal_ordering_commands_are_not_reachable_over_http()
    {
        var key = ShopperAccessTokens.CreateKey();
        await using var host = await ShopperHost.StartOrderingAsync(key);
        var token = ShopperAccessTokens.Issue(key, Guid.NewGuid(), "Alice").Value;
        string[] routes =
        [
            "/api/ordering/mark-order-paid",
            "/api/ordering/ship-order",
            "/api/ordering/mark-order-stock-confirmed",
            "/api/ordering/mark-order-awaiting-validation",
            "/api/ordering/cancel-order-for-insufficient-stock"
        ];

        foreach (var route in routes)
        {
            using var anonymous = await host.PostAsync(route, new { commandId = Guid.NewGuid(), orderId = Guid.NewGuid() }, token: null);
            using var signed = await host.PostAsync(route, new { commandId = Guid.NewGuid(), orderId = Guid.NewGuid() }, token);
            Assert.Equal(HttpStatusCode.NotFound, anonymous.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, signed.StatusCode);
        }
    }

    [Fact]
    public async Task Identity_sign_in_token_is_accepted_by_ordering()
    {
        await using var identity = await ShopperHost.StartIdentityAsync();
        var accountId = Guid.NewGuid();
        var token = await RegisterAndSignInAsync(identity, accountId);
        await using var ordering = await ShopperHost.StartOrderingFromIdentityAsync(identity.Address);

        using var own = await ordering.PostAsync("/api/ordering/place-order", Place(accountId), token);
        using var other = await ordering.PostAsync("/api/ordering/place-order", Place(Guid.NewGuid()), token);
        using var missing = await ordering.PostAsync("/api/ordering/place-order", Place(accountId), token: null);

        Assert.Equal(HttpStatusCode.Accepted, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
    }

    [Fact]
    public async Task Shopper_routes_reject_a_trailing_slash_a_large_body_and_a_missing_id()
    {
        var key = ShopperAccessTokens.CreateKey();
        await using var basket = await ShopperHost.StartBasketAsync(key);
        await using var ordering = await ShopperHost.StartOrderingAsync(key);
        var alice = Guid.NewGuid();
        var token = ShopperAccessTokens.Issue(key, alice, "Alice").Value;

        using var slash = await basket.GetAsync($"/views/basket-contents/?buyerId={alice:D}", token: null);
        using var placeSlash = await ordering.PostAsync("/api/ordering/place-order/", Place(alice), token: null);
        using var missingId = await basket.PostAsync(
            "/api/basket/add-item-to-basket",
            new { id = Guid.NewGuid(), productId = Guid.NewGuid(), productName = "Boot", unitPrice = 10m, quantity = 1 },
            token);
        using var large = await basket.Client.SendAsync(LargeBody(token));
        using var utf16 = await basket.Client.SendAsync(Utf16Body(token, alice));
        using var duplicateBuyer = await basket.Client.SendAsync(DuplicateBuyer(token, alice));

        Assert.Equal(HttpStatusCode.Unauthorized, slash.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, placeSlash.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingId.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, utf16.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateBuyer.StatusCode);
    }

    [Fact]
    public async Task Unknown_key_id_refreshes_the_jwks_document()
    {
        var first = ShopperAccessTokens.CreateKey();
        var second = ShopperAccessTokens.CreateKey();
        var published = new List<RsaSecurityKey> { first };
        var fetches = 0;
        await using var identity = await ShopperHost.StartJwksAsync(() =>
        {
            Interlocked.Increment(ref fetches);
            return ShopperAccessTokens.WritePublicJwks(published);
        });
        await using var basket = await ShopperHost.StartBasketFromIdentityAsync(identity.Address);
        var alice = Guid.NewGuid();
        var previous = ShopperAccessTokens.JwksRefreshInterval;
        ShopperAccessTokens.JwksRefreshInterval = TimeSpan.Zero;
        try
        {
            using var known = await basket.PostAsync("/api/basket/add-item-to-basket", AddItem(alice), ShopperAccessTokens.Issue(first, alice, "Alice").Value);
            published.Add(second);
            using var rotated = await basket.PostAsync("/api/basket/add-item-to-basket", AddItem(alice), ShopperAccessTokens.Issue(second, alice, "Alice").Value);

            Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
            Assert.True(rotated.StatusCode == HttpStatusCode.Accepted, fetches + " " + await rotated.Content.ReadAsStringAsync());
            Assert.True(fetches >= 2);
        }
        finally
        {
            ShopperAccessTokens.JwksRefreshInterval = previous;
        }
    }

    [Fact]
    public async Task Unknown_kids_share_one_refresh_inside_the_interval()
    {
        var key = ShopperAccessTokens.CreateKey();
        var fetches = 0;
        await using var identity = await ShopperHost.StartJwksAsync(() =>
        {
            Interlocked.Increment(ref fetches);
            return ShopperAccessTokens.WritePublicJwks(key);
        });
        await using var basket = await ShopperHost.StartBasketFromIdentityAsync(identity.Address);
        var alice = Guid.NewGuid();
        var previous = ShopperAccessTokens.JwksRefreshInterval;
        ShopperAccessTokens.JwksRefreshInterval = TimeSpan.FromMinutes(5);
        try
        {
            using var known = await basket.PostAsync(
                "/api/basket/add-item-to-basket",
                AddItem(alice),
                ShopperAccessTokens.Issue(key, alice, "Alice").Value);
            Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
            var afterKnown = Volatile.Read(ref fetches);

            for (var i = 0; i < 20; i++)
            {
                var stranger = ShopperAccessTokens.CreateKey();
                using var denied = await basket.PostAsync(
                    "/api/basket/add-item-to-basket",
                    AddItem(alice),
                    ShopperAccessTokens.Issue(stranger, alice, "Alice").Value);
                Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            }

            Assert.InRange(Volatile.Read(ref fetches) - afterKnown, 0, 1);
        }
        finally
        {
            ShopperAccessTokens.JwksRefreshInterval = previous;
        }
    }

    private static async Task<string> RegisterAndSignInAsync(ShopperHost identity, Guid accountId)
    {
        using var register = await identity.PostAsync("/api/identity/register-shopper-account", new
        {
            id = Guid.NewGuid(),
            accountId,
            userName = "alice-" + accountId.ToString("N")[..8],
            email = accountId.ToString("N")[..8] + "@example.com",
            password = Password,
            name = "Alice",
            lastName = "Smith"
        }, token: null);
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);

        using var signIn = await identity.PostAsync("/api/identity/sign-in-shopper", new
        {
            id = Guid.NewGuid(),
            userName = "alice-" + accountId.ToString("N")[..8],
            password = Password,
            rememberLogin = false
        }, token: null);
        var json = await signIn.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Accepted, signIn.StatusCode);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage LargeBody(string token)
    {
        var padding = new string('x', (64 * 1024) + 1);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/basket/add-item-to-basket")
        {
            Content = new StringContent("{\"buyerId\":\"" + padding + "\"}", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static HttpRequestMessage DuplicateBuyer(string token, Guid buyerId)
    {
        var json = "{\"buyerId\":\"" + buyerId.ToString("D") + "\",\"buyerId\":\"00000000-0000-0000-0000-000000000000\"}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/basket/add-item-to-basket")
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static HttpRequestMessage Utf16Body(string token, Guid buyerId)
    {
        var json = "{\"id\":\"" + Guid.NewGuid().ToString("D") + "\",\"buyerId\":\"" + buyerId.ToString("D") + "\",\"productId\":\"" + Guid.NewGuid().ToString("D") + "\",\"productName\":\"Boot\",\"unitPrice\":10,\"quantity\":1}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/basket/add-item-to-basket")
        {
            Content = new StringContent(json, System.Text.Encoding.Unicode, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string ProfileUrl(Guid accountId) =>
        $"{ShopperAccountProfileEndpoint.Route}?accountId={accountId:D}";

    private static object AddItem(Guid buyerId) => new
    {
        id = Guid.NewGuid(),
        buyerId,
        productId = Guid.NewGuid(),
        productName = "Alpine boot",
        unitPrice = 120m,
        quantity = 1
    };

    private static object Place(Guid buyerId) => Place(buyerId, Guid.NewGuid());

    private static object Place(Guid buyerId, Guid orderId) => new
        {
            commandId = orderId,
            orderId,
            buyerId,
            userName = "alice",
            street = "1 Main",
            city = "Seattle",
            state = "WA",
            country = "USA",
            zipCode = "98101",
            cardTypeId = 1,
            cardHolderName = "Alice",
            maskedCardNumber = "4444",
            cardExpiration = new DateTime(2028, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            orderItems = new[]
            {
                new { productId = Guid.NewGuid(), productName = "Alpine boot", unitPrice = 120m, units = 2 }
            }
        };

    private sealed class ShopperHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private ShopperHost(WebApplication app, HttpClient client, string address)
        {
            _app = app;
            Client = client;
            Address = address;
        }

        public HttpClient Client { get; }

        public string Address { get; }

        public IServiceProvider Services => _app.Services;

        public static Task<ShopperHost> StartBasketAsync(RsaSecurityKey key) =>
            StartAsync(
                key,
                static builder =>
                {
                    var context = AddContext(builder);
                    var assembly = typeof(BasketItemAdded).Assembly;
                    context.AddBasketContext(assembly);
                    builder.Services.AddLawnDartHttpCommands(assembly);
                },
                static app =>
                {
                    app.MapLawnDartCommands();
                    app.MapBasketContents();
                });

        public static Task<ShopperHost> StartOrderingAsync(RsaSecurityKey key) =>
            StartAsync(
                key,
                static builder =>
                {
                    var context = AddContext(builder);
                    var assembly = typeof(OrderPlaced).Assembly;
                    context.AddOrderingContext(assembly, typeof(OrderStarted).Assembly);
                    builder.Services.AddLawnDartHttpCommands(assembly);
                    builder.Services.AddScoped<IShopperOrderBuyerLookup, ShopperOrderBuyerLookup>();
                },
                static app =>
                {
                    app.MapLawnDartCommands();
                    app.MapOrdersByShopper();
                    app.MapOrderSummary();
                });

        public static Task<ShopperHost> StartProfileAsync(RsaSecurityKey key) =>
            StartAsync(
                key,
                static builder =>
                {
                    var context = AddContext(builder);
                    context.AddIdentityContext(typeof(ShopperAccountProfileProjection).Assembly);
                },
                static app => app.MapShopperAccountProfile());

        public static Task<ShopperHost> StartOrderingFromIdentityAsync(string identityAddress)
        {
            var builder = NewBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Url"] = identityAddress
            });
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            var assembly = typeof(OrderPlaced).Assembly;
            context.AddOrderingContext(assembly, typeof(OrderStarted).Assembly);
            builder.Services.AddLawnDartHttpCommands(assembly);
            builder.Services.AddScoped<IShopperOrderBuyerLookup, ShopperOrderBuyerLookup>();
            builder.Services.AddShopperBearerAccess(builder.Configuration);
            return ListenAsync(builder, static app =>
            {
                app.MapLawnDartCommands();
                app.MapOrdersByShopper();
            });
        }

        public static async Task<ShopperHost> StartJwksAsync(Func<string> document)
        {
            var builder = NewBuilder();
            var app = builder.Build();
            app.MapGet(ShopperAccessTokens.JwksPath, () => Results.Text(document(), "application/json"));
            await app.StartAsync().ConfigureAwait(false);
            var address = AddressOf(app);
            return new ShopperHost(app, new HttpClient { BaseAddress = new Uri(address) }, address);
        }

        public static async Task<ShopperHost> StartBasketFromIdentityAsync(string identityAddress)
        {
            var builder = NewBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Url"] = identityAddress
            });
            WireBasket(builder);
            builder.Services.AddShopperBearerAccess(builder.Configuration);
            return await ListenAsync(builder, static app =>
            {
                app.MapLawnDartCommands();
                app.MapBasketContents();
            }).ConfigureAwait(false);
        }

        public static async Task<ShopperHost> StartIdentityAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync().ConfigureAwait(false);
            var builder = NewBuilder();
            builder.Services.AddSingleton(connection);
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
            var assembly = typeof(ShopperAccountRegistered).Assembly;
            context.AddIdentityContext(assembly);
            builder.Services.AddLawnDartHttpCommands(assembly);
            builder.Services.AddShopperAccessTokens();
            builder.AddEShopSlices(assembly);
            var app = builder.Build();
            await app.InitializeShopperLoginStoreAsync().ConfigureAwait(false);
            app.MapLawnDartCommands();
            app.MapShopperJwks();
            app.MapEShopSlices(assembly);
            await app.StartAsync().ConfigureAwait(false);
            var address = AddressOf(app);
            return new ShopperHost(app, new HttpClient { BaseAddress = new Uri(address) }, address);
        }

        public Task<HttpResponseMessage> PostAsync(string path, object body, string? token) =>
            SendAsync(HttpMethod.Post, path, body, token);

        public Task<HttpResponseMessage> GetAsync(string path, string? token) =>
            SendAsync(HttpMethod.Get, path, body: null, token);

        public async Task<(HttpStatusCode Status, string Body)> PollAsync(string path, string token, HttpStatusCode expected)
        {
            string body = "";
            var status = HttpStatusCode.NotFound;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                using var response = await GetAsync(path, token).ConfigureAwait(false);
                status = response.StatusCode;
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (status == expected)
                    return (status, body);

                await Task.Delay(100).ConfigureAwait(false);
            }

            return (status, body);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync().ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, string? token)
        {
            var request = new HttpRequestMessage(method, path);
            if (body is not null)
                request.Content = JsonContent.Create(body);
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        private static Task<ShopperHost> StartAsync(
            RsaSecurityKey key,
            Action<WebApplicationBuilder> configure,
            Action<WebApplication> map)
        {
            var builder = NewBuilder();
            configure(builder);
            builder.Services.AddShopperBearerAccess(key);
            return ListenAsync(builder, map);
        }

        private static BoundedContextBuilder AddContext(WebApplicationBuilder builder)
        {
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            return context;
        }

        private static void WireBasket(WebApplicationBuilder builder)
        {
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            var assembly = typeof(BasketItemAdded).Assembly;
            context.AddBasketContext(assembly);
            builder.Services.AddLawnDartHttpCommands(assembly);
        }

        private static WebApplicationBuilder NewBuilder()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            return builder;
        }

        private static async Task<ShopperHost> ListenAsync(WebApplicationBuilder builder, Action<WebApplication> map)
        {
            var app = builder.Build();
            map(app);
            await app.StartAsync().ConfigureAwait(false);
            var address = AddressOf(app);
            return new ShopperHost(app, new HttpClient { BaseAddress = new Uri(address) }, address);
        }

        private static string AddressOf(WebApplication app) =>
            app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }
}
