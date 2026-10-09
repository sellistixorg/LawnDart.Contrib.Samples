using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Requires a shopper access token on shopper routes.
/// </summary>
/// <remarks>
/// LawnDart <c>HttpCommandOptions</c> has no endpoint filter. This middleware runs after routing
/// and classifies the matched route pattern. A trailing slash or a different case still selects
/// that pattern. The middleware sets <c>HttpContext.User</c> so LawnDart can copy <c>sub</c>.
/// A buyer id or account id on the request must equal that <c>sub</c>.
/// </remarks>
public static class ShopperBearerAccessExtensions
{
    /// <summary>
    /// Validates bearer tokens with one local RSA key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="signingKey">RSA key whose public part verifies the token.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddShopperBearerAccess(
        this IServiceCollection services,
        SecurityKey signingKey)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(signingKey);
        AddScheme(services);
        services.AddOptions<JwtBearerOptions>(ShopperAccessTokens.AuthenticationScheme)
            .Configure(options =>
            {
                options.TokenValidationParameters.IssuerSigningKey = signingKey;
            });
        return services;
    }

    /// <summary>
    /// Validates bearer tokens with the JWKS document at <c>Identity:Url</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Host configuration. AppHost sets <c>Identity__Url</c> from the Identity endpoint.</param>
    /// <returns>The same service collection.</returns>
    /// <remarks>
    /// A missing or relative URL still lets the host start. Tokens then fail validation.
    /// Key loading runs in <c>OnMessageReceived</c>. The signing-key resolver only reads the cache.
    /// Any fetch waits <see cref="ShopperAccessTokens.JwksRefreshInterval"/> before another try.
    /// A failed fetch also waits <see cref="ShopperAccessTokens.JwksFailureCooldown"/>.
    /// </remarks>
    public static IServiceCollection AddShopperBearerAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton<ShopperJwksCache>();
        AddScheme(services);
        services.AddOptions<JwtBearerOptions>(ShopperAccessTokens.AuthenticationScheme)
            .Configure<ShopperJwksCache>((options, cache) =>
            {
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context => cache.EnsureAsync(
                        ShopperTokenKid.Read(ShopperTokenKid.FromContext(context)),
                        context.HttpContext.RequestAborted)
                };
                options.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, kid, _) => cache.Resolve(kid);
            });
        return services;
    }

    /// <summary>
    /// Validates bearer tokens with keys already loaded in memory.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="keys">In-memory public keys. The resolver does not load them.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddShopperBearerAccess(
        this IServiceCollection services,
        ShopperValidationKeys keys)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(keys);
        services.AddSingleton(keys);
        AddScheme(services);
        services.AddOptions<JwtBearerOptions>(ShopperAccessTokens.AuthenticationScheme)
            .Configure<ShopperValidationKeys>((options, stored) =>
            {
                options.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, kid, _) => stored.Resolve(kid);
            });
        return services;
    }

    private static void AddScheme(IServiceCollection services)
    {
        services.AddAuthentication()
            .AddJwtBearer(ShopperAccessTokens.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = ShopperAccessTokens.ValidationParameters();
            });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, ShopperBearerAccessStartup>());
    }
}

/// <summary>
/// Public keys Identity has already loaded. The bearer resolver reads this list and does not query the database.
/// </summary>
public sealed class ShopperValidationKeys
{
    private IReadOnlyList<SecurityKey> _keys = [];

    /// <summary>
    /// Replaces the in-memory keys. Do not log key material.
    /// </summary>
    /// <param name="keys">Public keys from the login database.</param>
    public void Replace(IReadOnlyList<SecurityKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _keys = keys;
    }

    /// <summary>
    /// Keys whose id matches <paramref name="kid"/>, or every key when <paramref name="kid"/> is blank.
    /// </summary>
    /// <param name="kid">Key id from the token header.</param>
    /// <returns>The matching keys. Empty when none match.</returns>
    public IEnumerable<SecurityKey> Resolve(string? kid)
    {
        if (string.IsNullOrEmpty(kid))
            return _keys;

        return _keys.Where(key => string.Equals(key.KeyId, kid, StringComparison.Ordinal));
    }
}

/// <summary>
/// Reads the key id from a compact JWT header. The token is not logged.
/// </summary>
internal static class ShopperTokenKid
{
    /// <summary>
    /// Returns the bearer token from the message event, or from the Authorization header.
    /// </summary>
    /// <param name="context">The bearer message event.</param>
    /// <returns>The compact token, or null when the request has none.</returns>
    public static string? FromContext(MessageReceivedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!string.IsNullOrEmpty(context.Token))
            return context.Token;

        var header = context.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? header[prefix.Length..].Trim()
            : null;
    }

    /// <summary>
    /// Reads the <c>kid</c> header field.
    /// </summary>
    /// <param name="token">Compact JWT.</param>
    /// <returns>The key id, or null when the header has none.</returns>
    public static string? Read(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        var dot = token.IndexOf('.');
        if (dot <= 0)
            return null;

        try
        {
            var json = Base64UrlEncoder.Decode(token[..dot]);
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("kid", out var kid) && kid.ValueKind == JsonValueKind.String
                ? kid.GetString()
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}

/// <summary>
/// Reads buyer and account ids from a shopper request.
/// </summary>
internal static class ShopperSubjectIds
{
    internal static readonly string[] Names = ["buyerId", "accountId"];

    /// <summary>
    /// Reads subject ids from a JSON object.
    /// </summary>
    /// <param name="json">Request body.</param>
    /// <returns>The ids. An empty or non-Guid subject value is marked invalid.</returns>
    internal static ShopperIdRead FromJson(string? json)
    {
        var read = new ShopperIdRead();
        if (string.IsNullOrWhiteSpace(json))
        {
            read.InvalidJson = true;
            return read;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            read.InvalidJson = true;
            return read;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                read.InvalidJson = true;
                return read;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("orderId", StringComparison.OrdinalIgnoreCase))
                    read.OrderId = ReadGuid(property.Value, read, subject: false);

                if (!Names.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                    continue;

                read.SawSubject = true;
                var id = ReadGuid(property.Value, read, subject: true);
                if (id is Guid value && value != Guid.Empty)
                    read.Ids.Add(value);
            }
        }

        return read;
    }

    /// <summary>
    /// Reads subject ids from the query string.
    /// </summary>
    /// <param name="query">Request query.</param>
    /// <returns>The ids. An empty or non-Guid subject value is marked invalid.</returns>
    internal static ShopperIdRead FromQuery(IQueryCollection query)
    {
        var read = new ShopperIdRead();
        foreach (var name in Names)
        {
            if (!query.TryGetValue(name, out var values))
                continue;

            foreach (var value in values)
            {
                read.SawSubject = true;
                if (Guid.TryParse(value, out var id) && id != Guid.Empty)
                    read.Ids.Add(id);
                else
                    read.EmptyOrInvalid = true;
            }
        }

        return read;
    }

    private static Guid? ReadGuid(JsonElement value, ShopperIdRead read, bool subject)
    {
        if (value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id))
        {
            if (subject && id == Guid.Empty)
                read.EmptyOrInvalid = true;
            return id;
        }

        if (subject)
            read.EmptyOrInvalid = true;
        return null;
    }
}

/// <summary>
/// Subject ids found on one shopper request.
/// </summary>
internal sealed class ShopperIdRead
{
    /// <summary>
    /// Buyer or account ids that are non-empty Guids.
    /// </summary>
    public List<Guid> Ids { get; } = [];

    /// <summary>
    /// True when a buyer or account field was present.
    /// </summary>
    public bool SawSubject { get; set; }

    /// <summary>
    /// True when a buyer or account value was empty or not a Guid.
    /// </summary>
    public bool EmptyOrInvalid { get; set; }

    /// <summary>
    /// True when the body was not a JSON object.
    /// </summary>
    public bool InvalidJson { get; set; }

    /// <summary>
    /// Order id from the body, when that field was a Guid.
    /// </summary>
    public Guid? OrderId { get; set; }
}

/// <summary>
/// Cached public keys from Identity. A fetch, successful or not, blocks another fetch until the refresh interval has passed.
/// </summary>
internal sealed class ShopperJwksCache
{
    private readonly HttpClient _http = new() { Timeout = ShopperAccessTokens.JwksFetchTimeout };
    private readonly string? _address;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<SecurityKey> _keys = [];
    private long _fetchedAtTicks;
    private long _backoffUntilTicks;

    /// <summary>
    /// Creates the cache from <c>Identity:Url</c>.
    /// </summary>
    /// <param name="configuration">Host configuration.</param>
    public ShopperJwksCache(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var url = configuration[$"{EShopIdentityClientOptions.SectionName}:Url"];
        if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var identity))
            _address = new Uri(identity, ShopperAccessTokens.JwksPath).AbsoluteUri;
    }

    /// <summary>
    /// Fetches keys when the refresh interval has elapsed. An unknown key id does not start its own fetch.
    /// </summary>
    /// <param name="kid">Key id from the token. It does not bypass the interval.</param>
    /// <param name="cancellationToken">Token used to cancel the wait.</param>
    /// <returns>A task that completes when the cache is current for this interval.</returns>
    public async Task EnsureAsync(string? kid, CancellationToken cancellationToken)
    {
        _ = kid;
        if (_address is null || !RefreshDue())
            return;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!RefreshDue())
                return;

            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Returns cached keys for <paramref name="kid"/>. This does not fetch.
    /// </summary>
    /// <param name="kid">Key id from the token. An empty id returns every cached key.</param>
    /// <returns>The matching keys. An unknown id returns none.</returns>
    public IEnumerable<SecurityKey> Resolve(string? kid)
    {
        if (string.IsNullOrEmpty(kid))
            return _keys;

        return _keys.Where(key => string.Equals(key.KeyId, kid, StringComparison.Ordinal));
    }

    private bool RefreshDue()
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var backoff = Volatile.Read(ref _backoffUntilTicks);
        if (backoff != 0 && now < backoff)
            return false;

        var fetched = Volatile.Read(ref _fetchedAtTicks);
        var interval = ShopperAccessTokens.JwksRefreshInterval.Ticks;
        return fetched == 0 || now - fetched >= interval;
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ShopperAccessTokens.JwksFetchTimeout);
        try
        {
            using var response = await _http.GetAsync(_address, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                BackOff();
                return;
            }

            var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            var set = new JsonWebKeySet(json);
            _keys = set.GetSigningKeys().ToList();
            Volatile.Write(ref _fetchedAtTicks, DateTimeOffset.UtcNow.UtcTicks);
            Volatile.Write(ref _backoffUntilTicks, 0);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or ArgumentException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;

            BackOff();
        }
    }

    private void BackOff()
    {
        var now = DateTimeOffset.UtcNow;
        Volatile.Write(ref _fetchedAtTicks, now.UtcTicks);
        Volatile.Write(ref _backoffUntilTicks, now.Add(ShopperAccessTokens.JwksFailureCooldown).UtcTicks);
    }
}

/// <summary>
/// Places the shopper access middleware after routing.
/// </summary>
internal sealed class ShopperBearerAccessStartup : IStartupFilter
{
    /// <summary>
    /// Inserts routing and the shopper gate before the rest of the pipeline.
    /// </summary>
    /// <param name="next">The next startup filter.</param>
    /// <returns>The pipeline that runs routing, then the shopper gate, then <paramref name="next"/>.</returns>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseRouting();
            app.UseMiddleware<ShopperBearerAccessMiddleware>();
            next(app);
        };
    }
}

/// <summary>
/// Requires a valid shopper access token on shopper routes and rejects a shopper id that does not match it.
/// </summary>
internal sealed class ShopperBearerAccessMiddleware(RequestDelegate next)
{
    private const int MaxBodyBytes = 64 * 1024;

    /// <summary>
    /// Classifies the route. A hidden command is 404. A bad body is 400, 413, or 415. A missing token is 401. A mismatched shopper id is 403.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <returns>A task that completes when the response is finished.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var route = ShopperAccessRoutes.Classify(context);
        if (route == ShopperRouteKind.Hidden)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, "Not Found", "That command is not available over HTTP.")
                .ConfigureAwait(false);
            return;
        }

        if (route == ShopperRouteKind.Open)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var result = await context.AuthenticateAsync(ShopperAccessTokens.AuthenticationScheme).ConfigureAwait(false);
        var accountId = result.Succeeded ? ShopperAccessTokens.ReadAccountId(result.Principal) : null;
        if (accountId is not Guid subject)
        {
            await WriteAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized", "A valid shopper access token is required.")
                .ConfigureAwait(false);
            return;
        }

        context.User = result.Principal!;
        var body = await ReadAsync(context.Request, context.RequestAborted).ConfigureAwait(false);
        if (body.Status == ShopperBodyStatus.TooLarge)
        {
            await WriteAsync(context, StatusCodes.Status413PayloadTooLarge, "Payload Too Large", "The request body is larger than 64 KB.")
                .ConfigureAwait(false);
            return;
        }

        if (body.Status == ShopperBodyStatus.UnsupportedCharset)
        {
            await WriteAsync(context, StatusCodes.Status415UnsupportedMediaType, "Unsupported Media Type", "The request body must be UTF-8.")
                .ConfigureAwait(false);
            return;
        }

        if (body.Status == ShopperBodyStatus.InvalidJson)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, "Bad Request", "The request body is not valid JSON.")
                .ConfigureAwait(false);
            return;
        }

        if (body.InvalidSubject)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, "Bad Request", "The shopper id is required.")
                .ConfigureAwait(false);
            return;
        }

        if (body.Ids.Any(id => id != subject))
        {
            await WriteAsync(context, StatusCodes.Status403Forbidden, "Forbidden", "The shopper id does not match the access token.")
                .ConfigureAwait(false);
            return;
        }

        if (route == ShopperRouteKind.CancelOrder)
        {
            if (body.OrderId is not Guid orderId || orderId == Guid.Empty)
            {
                await WriteAsync(context, StatusCodes.Status400BadRequest, "Bad Request", "The order id is required.")
                    .ConfigureAwait(false);
                return;
            }

            var lookup = context.RequestServices.GetService<IShopperOrderBuyerLookup>();
            var buyer = lookup is null
                ? null
                : await lookup.FindBuyerIdAsync(orderId, context.RequestAborted).ConfigureAwait(false);
            if (buyer is not Guid owner)
            {
                await WriteAsync(context, StatusCodes.Status404NotFound, "Not Found", "That order was not found.")
                    .ConfigureAwait(false);
                return;
            }

            if (owner != subject)
            {
                await WriteAsync(context, StatusCodes.Status403Forbidden, "Forbidden", "The shopper id does not match the access token.")
                    .ConfigureAwait(false);
                return;
            }
        }
        else if (body.Ids.Count == 0)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, "Bad Request", "The shopper id is required.")
                .ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static async Task<ShopperBody> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var query = ShopperSubjectIds.FromQuery(request.Query);
        if (!HasBody(request))
        {
            return new ShopperBody
            {
                Status = ShopperBodyStatus.Ok,
                Ids = query.Ids,
                OrderId = query.OrderId,
                InvalidSubject = query.EmptyOrInvalid
            };
        }

        if (request.ContentLength > MaxBodyBytes)
            return ShopperBody.TooLarge();

        if (!IsUtf8(request.ContentType))
            return ShopperBody.UnsupportedCharset();

        request.EnableBuffering(bufferThreshold: MaxBodyBytes);
        request.Body.Position = 0;
        var buffer = new byte[MaxBodyBytes + 1];
        var read = 0;
        while (read <= MaxBodyBytes)
        {
            var count = await request.Body.ReadAsync(buffer.AsMemory(read, buffer.Length - read), cancellationToken)
                .ConfigureAwait(false);
            if (count == 0)
                break;

            read += count;
        }

        request.Body.Position = 0;
        if (read > MaxBodyBytes)
            return ShopperBody.TooLarge();

        var json = Encoding.UTF8.GetString(buffer, 0, read);
        var parsed = ShopperSubjectIds.FromJson(json);
        if (parsed.InvalidJson)
            return ShopperBody.InvalidJson();

        var ids = new List<Guid>(query.Ids);
        ids.AddRange(parsed.Ids);
        return new ShopperBody
        {
            Status = ShopperBodyStatus.Ok,
            Ids = ids,
            OrderId = parsed.OrderId ?? query.OrderId,
            InvalidSubject = query.EmptyOrInvalid || parsed.EmptyOrInvalid
        };
    }

    private static bool HasBody(HttpRequest request) =>
        HttpMethods.IsPost(request.Method)
        || HttpMethods.IsPut(request.Method)
        || HttpMethods.IsPatch(request.Method);

    private static bool IsUtf8(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return true;

        foreach (var part in contentType.Split(';'))
        {
            var piece = part.Trim();
            if (!piece.StartsWith("charset", StringComparison.OrdinalIgnoreCase))
                continue;

            var split = piece.IndexOf('=');
            if (split < 0)
                return false;

            var charset = piece[(split + 1)..].Trim().Trim('"');
            return charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase)
                || charset.Equals("utf8", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static Task WriteAsync(HttpContext context, int status, string title, string detail)
    {
        if (status == StatusCodes.Status401Unauthorized)
            context.Response.Headers.WWWAuthenticate = "Bearer";

        return Results.Problem(title: title, detail: detail, statusCode: status).ExecuteAsync(context);
    }
}

/// <summary>
/// How the shopper gate treats one route.
/// </summary>
internal enum ShopperRouteKind
{
    /// <summary>
    /// Catalog, register, and sign-in. No token is required.
    /// </summary>
    Open,

    /// <summary>
    /// A shopper command or view. The token subject must match the buyer or account id.
    /// </summary>
    Shopper,

    /// <summary>
    /// Cancel order. The token subject must own the order.
    /// </summary>
    CancelOrder,

    /// <summary>
    /// An internal ordering command. HTTP gets 404.
    /// </summary>
    Hidden
}

/// <summary>
/// Result of reading a shopper request body.
/// </summary>
internal enum ShopperBodyStatus
{
    /// <summary>
    /// The body was read, or the request had no body.
    /// </summary>
    Ok,

    /// <summary>
    /// The body is larger than 64 KB.
    /// </summary>
    TooLarge,

    /// <summary>
    /// The charset is not UTF-8.
    /// </summary>
    UnsupportedCharset,

    /// <summary>
    /// The body is not a JSON object.
    /// </summary>
    InvalidJson
}

/// <summary>
/// Shopper ids and body errors from one request.
/// </summary>
internal sealed class ShopperBody
{
    /// <summary>
    /// Whether the body could be read.
    /// </summary>
    public ShopperBodyStatus Status { get; init; }

    /// <summary>
    /// Buyer and account ids that are non-empty Guids.
    /// </summary>
    public List<Guid> Ids { get; init; } = [];

    /// <summary>
    /// True when any buyer or account value was empty or not a Guid.
    /// </summary>
    public bool InvalidSubject { get; init; }

    /// <summary>
    /// Order id from the body or the query, when that field was a Guid.
    /// </summary>
    public Guid? OrderId { get; init; }

    /// <summary>
    /// A body larger than 64 KB.
    /// </summary>
    /// <returns>The too-large result.</returns>
    public static ShopperBody TooLarge() => new() { Status = ShopperBodyStatus.TooLarge };

    /// <summary>
    /// A body whose charset is not UTF-8.
    /// </summary>
    /// <returns>The unsupported-charset result.</returns>
    public static ShopperBody UnsupportedCharset() => new() { Status = ShopperBodyStatus.UnsupportedCharset };

    /// <summary>
    /// A body that is not a JSON object.
    /// </summary>
    /// <returns>The invalid-JSON result.</returns>
    public static ShopperBody InvalidJson() => new() { Status = ShopperBodyStatus.InvalidJson };
}

/// <summary>
/// Maps a request path to a <see cref="ShopperRouteKind"/>.
/// </summary>
internal static class ShopperAccessRoutes
{
    private static readonly string[] Hidden =
    [
        "/api/ordering/mark-order-paid",
        "/api/ordering/ship-order",
        "/api/ordering/mark-order-stock-confirmed",
        "/api/ordering/mark-order-awaiting-validation",
        "/api/ordering/cancel-order-for-insufficient-stock"
    ];

    /// <summary>
    /// Classifies the matched route pattern and the request path. A trailing slash does not change the kind.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <returns>The route kind.</returns>
    public static ShopperRouteKind Classify(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pattern = Pattern(context);
        var path = Canonical(context.Request.Path);
        var candidate = pattern ?? path;
        if (IsHidden(candidate) || IsHidden(path))
            return ShopperRouteKind.Hidden;

        if (IsCancel(candidate) || IsCancel(path))
            return ShopperRouteKind.CancelOrder;

        if (IsShopper(candidate) || IsShopper(path))
            return ShopperRouteKind.Shopper;

        return ShopperRouteKind.Open;
    }

    private static string? Pattern(HttpContext context)
    {
        if (context.GetEndpoint() is not RouteEndpoint endpoint)
            return null;

        var raw = endpoint.RoutePattern.RawText;
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (!raw.StartsWith('/'))
            raw = "/" + raw;

        return Canonical(raw);
    }

    private static string Canonical(PathString path) => Canonical(path.Value);

    private static string Canonical(string? path)
    {
        var value = string.IsNullOrEmpty(path) ? "/" : path;
        if (value.Length > 1)
            value = value.TrimEnd('/');

        return value;
    }

    private static bool IsHidden(string path) =>
        Hidden.Any(route => path.Equals(route, StringComparison.OrdinalIgnoreCase));

    private static bool IsCancel(string path) =>
        path.Equals("/api/ordering/cancel-order", StringComparison.OrdinalIgnoreCase);

    private static bool IsShopper(string path)
    {
        if (path.StartsWith("/api/basket", StringComparison.OrdinalIgnoreCase))
            return true;

        if (path.Equals("/api/ordering/place-order", StringComparison.OrdinalIgnoreCase))
            return true;

        if (path.Equals("/views/basket-contents", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/views/orders-by-shopper", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/views/shopper-account-profile", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/views/order-summary", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!path.StartsWith("/api/identity", StringComparison.OrdinalIgnoreCase))
            return false;

        if (path.Equals("/api/identity/sign-in-shopper", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/identity/register-shopper-account", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
