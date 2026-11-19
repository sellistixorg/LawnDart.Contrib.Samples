using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

/// <summary>
/// Tests the cache that holds Identity's public signing keys.
/// </summary>
[Collection(ShopperJwksSettings.Collection)]
public class ShopperJwksCacheTests
{
    private static readonly Uri Address = new("http://identity.test" + ShopperAccessTokens.JwksPath);

    [Fact]
    public async Task Slow_first_fetch_still_loads_the_keys()
    {
        var key = Key("first");
        var identity = new StubIdentity(async (_, cancellationToken) =>
        {
            // Longer than the old two second limit.
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            return Jwks(key);
        });
        var cache = identity.Cache();

        await cache.EnsureAsync("first", CancellationToken.None);

        Assert.Single(cache.Resolve("first"));
        Assert.Equal(1, identity.Calls);
    }

    [Fact]
    public async Task First_fetch_is_retried_after_a_failure()
    {
        var key = Key("first");
        var identity = new StubIdentity((call, _) => Task.FromResult(
            call == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Jwks(key)));
        var cache = identity.Cache();

        await cache.EnsureAsync("first", CancellationToken.None);

        Assert.Single(cache.Resolve("first"));
        Assert.Equal(2, identity.Calls);
    }

    [Fact]
    public async Task Failed_refresh_keeps_the_last_good_keys()
    {
        var key = Key("first");
        var failing = false;
        var identity = new StubIdentity((_, _) => Task.FromResult(
            Volatile.Read(ref failing) ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Jwks(key)));
        var cache = identity.Cache();
        await cache.EnsureAsync("first", CancellationToken.None);
        Volatile.Write(ref failing, true);

        var refreshed = await cache.RefreshAsync();

        Assert.False(refreshed);
        Assert.Equal(1 + ShopperAccessTokens.JwksFetchAttempts, identity.Calls);
        Assert.Single(cache.Resolve("first"));
    }

    [Fact]
    public async Task No_keys_after_a_failure_lets_the_next_token_fetch_again()
    {
        var key = Key("first");
        var down = true;
        var identity = new StubIdentity((_, _) => Task.FromResult(
            Volatile.Read(ref down) ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Jwks(key)));
        var cache = identity.Cache();
        await cache.EnsureAsync("first", CancellationToken.None);
        Assert.Empty(cache.Resolve("first"));

        Volatile.Write(ref down, false);
        await cache.EnsureAsync("first", CancellationToken.None);

        Assert.Single(cache.Resolve("first"));
    }

    [Fact]
    public async Task Unknown_key_id_triggers_one_refetch()
    {
        var first = Key("first");
        var second = Key("second");
        var published = new[] { first };
        var identity = new StubIdentity((_, _) => Task.FromResult(Jwks(Volatile.Read(ref published))));
        var cache = identity.Cache();
        var previous = ShopperAccessTokens.JwksUnknownKeyCooldown;
        ShopperAccessTokens.JwksUnknownKeyCooldown = TimeSpan.FromMinutes(5);
        try
        {
            await cache.EnsureAsync("first", CancellationToken.None);
            await cache.EnsureAsync("first", CancellationToken.None);
            Assert.Equal(1, identity.Calls);

            // The cooldown only limits refetches for unknown ids, so the first unknown id waits behind a fresh fetch.
            ShopperAccessTokens.JwksUnknownKeyCooldown = TimeSpan.Zero;
            Volatile.Write(ref published, [first, second]);
            await cache.EnsureAsync("second", CancellationToken.None);
            Assert.Equal(2, identity.Calls);
            Assert.Single(cache.Resolve("second"));

            ShopperAccessTokens.JwksUnknownKeyCooldown = TimeSpan.FromMinutes(5);
            await cache.EnsureAsync("stranger", CancellationToken.None);
            await cache.EnsureAsync("stranger", CancellationToken.None);
            Assert.Equal(2, identity.Calls);
            Assert.Empty(cache.Resolve("stranger"));
        }
        finally
        {
            ShopperAccessTokens.JwksUnknownKeyCooldown = previous;
        }
    }

    [Fact]
    public async Task Concurrent_tokens_share_one_fetch()
    {
        var key = Key("first");
        var identity = new StubIdentity(async (_, cancellationToken) =>
        {
            await Task.Delay(200, cancellationToken);
            return Jwks(key);
        });
        var cache = identity.Cache();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cache.EnsureAsync("first", CancellationToken.None)));

        Assert.Equal(1, identity.Calls);
        Assert.Single(cache.Resolve("first"));
    }

    [Fact]
    public async Task A_caller_that_gives_up_does_not_cancel_the_shared_fetch()
    {
        var key = Key("first");
        var identity = new StubIdentity(async (_, cancellationToken) =>
        {
            await Task.Delay(500, cancellationToken);
            return Jwks(key);
        });
        var cache = identity.Cache();
        using var aborted = new CancellationTokenSource(50);

        await cache.EnsureAsync("first", aborted.Token);
        Assert.Empty(cache.Resolve("first"));

        await cache.EnsureAsync("first", CancellationToken.None);
        Assert.Single(cache.Resolve("first"));
        Assert.Equal(1, identity.Calls);
    }

    private static RsaSecurityKey Key(string kid)
    {
        var key = ShopperAccessTokens.CreateKey();
        key.KeyId = kid;
        return key;
    }

    private static HttpResponseMessage Jwks(params RsaSecurityKey[] keys) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(ShopperAccessTokens.WritePublicJwks(keys), Encoding.UTF8, "application/json")
        };

    private sealed class StubIdentity(Func<int, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public ShopperJwksCache Cache() => new(Address, new HttpClient(this), NullLogger.Instance);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(Address, request.RequestUri);
            return respond(Interlocked.Increment(ref _calls), cancellationToken);
        }
    }
}

/// <summary>
/// Tests that change the static JWKS timings run one at a time.
/// </summary>
[CollectionDefinition(Collection)]
public sealed class ShopperJwksSettings
{
    public const string Collection = "Shopper JWKS settings";
}
