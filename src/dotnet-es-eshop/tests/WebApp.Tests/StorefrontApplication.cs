using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.WebApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.WebApp.Tests;

public sealed class StorefrontApplication : WebApplicationFactory<Program>
{
    public IdentityStub Identity { get; } = new();

    public List<string?> BasketAuthorization { get; } = [];

    public List<string?> CatalogAuthorization { get; } = [];

    public BasketApiStub Basket { get; }

    public CatalogApiStub Catalog { get; }

    public List<string?> OrderingAuthorization { get; } = [];

    public StorefrontApplication()
    {
        Basket = new BasketApiStub(BasketAuthorization);
        Catalog = new CatalogApiStub(CatalogAuthorization);
    }

    public CollectingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("IdentityUrl", "http://identity.test/");
        builder.UseSetting("services:basket-api:http:0", "http://127.0.0.1");
        builder.UseSetting("services:basket-api:https:0", "http://127.0.0.1");
        builder.UseSetting("services:catalog-api:http:0", "http://127.0.0.1");
        builder.UseSetting("services:catalog-api:https:0", "http://127.0.0.1");
        builder.UseSetting("services:ordering-api:http:0", "http://127.0.0.1");
        builder.UseSetting("services:ordering-api:https:0", "http://127.0.0.1");
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureServices(services =>
        {
            services.AddHttpClient(EShopStorefrontClients.Identity)
                .ConfigurePrimaryHttpMessageHandler(() => Identity);
            services.AddHttpClient(EShopStorefrontClients.Basket)
                .ConfigurePrimaryHttpMessageHandler(() => Basket);
            services.AddHttpClient(EShopStorefrontClients.Catalog)
                .ConfigurePrimaryHttpMessageHandler(() => Catalog);
            services.AddHttpClient(EShopStorefrontClients.Ordering)
                .ConfigurePrimaryHttpMessageHandler(() => new StaticJsonHandler(EmptyOrders, OrderingAuthorization));
        });
    }

    public HttpClient CreateNoRedirectClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

    private const string EmptyOrders =
        """{"buyerId":"00000000-0000-0000-0000-000000000000","orders":[]}""";
}

public sealed class IdentityStub : HttpMessageHandler
{
    public static readonly Guid AliceId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public const string UserName = "alice";

    public const string Password = "Pass123$";

    public const string AccessToken = "shopper-access-token-for-test";

    public static readonly DateTimeOffset AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);

    public DateTimeOffset NextAccessTokenExpiresAt { get; set; } = AccessTokenExpiresAt;

    public List<string> AuthorizationHeaders { get; } = [];

    public Guid? SignedOutAccount { get; private set; }

    public void Reset()
    {
        SignedOutAccount = null;
        NextAccessTokenExpiresAt = AccessTokenExpiresAt;
        AuthorizationHeaders.Clear();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        if (request.Headers.Authorization is { } authorization)
            AuthorizationHeaders.Add(authorization.ToString());

        var json = request.Content is null
            ? ""
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (path.EndsWith("/sign-in-shopper", StringComparison.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(json);
            var user = document.RootElement.GetProperty("userName").GetString();
            var password = document.RootElement.GetProperty("password").GetString();
            if (string.Equals(user, UserName, StringComparison.Ordinal) && password == Password)
            {
                return Json(HttpStatusCode.Accepted, new
                {
                    accountId = AliceId,
                    displayName = "Alice Smith",
                    accessToken = AccessToken,
                    accessTokenExpiresAt = NextAccessTokenExpiresAt
                });
            }

            return Json(HttpStatusCode.UnprocessableEntity, new
            {
                title = "INVALID_CREDENTIALS",
                detail = ShopperSignInClient.InvalidCredentialsMessage
            });
        }

        if (path.EndsWith("/sign-out-shopper", StringComparison.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(json);
            SignedOutAccount = document.RootElement.GetProperty("accountId").GetGuid();
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    protected override void Dispose(bool disposing)
    {
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(body)
        };
        return response;
    }
}

public sealed class BasketApiStub : HttpMessageHandler
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, List<BasketLine>> _lines = new();
    private readonly List<string?> _authorization;

    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    public BasketApiStub()
        : this([])
    {
    }

    public BasketApiStub(List<string?> authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        _authorization = authorization;
    }

    public void Reset()
    {
        lock (_gate)
            _lines.Clear();
    }

    public int QuantityOf(Guid buyerId, Guid productId)
    {
        lock (_gate)
        {
            foreach (var line in Lines(buyerId))
            {
                if (line.ProductId == productId)
                    return line.Quantity;
            }
        }

        return 0;
    }

    public void Seed(Guid buyerId, Guid productId, string name, decimal price, int quantity, string? pictureUrl)
    {
        lock (_gate)
        {
            var lines = Lines(buyerId);
            lines.RemoveAll(line => line.ProductId == productId);
            if (quantity > 0)
                lines.Add(new BasketLine(productId, name, price, quantity, pictureUrl));
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Yield so a caller that used ConfigureAwait(false) resumes off its renderer.
        await Task.Yield();
        lock (_authorization)
            _authorization.Add(request.Headers.Authorization?.ToString());
        if (Status != HttpStatusCode.OK && Status != HttpStatusCode.Accepted)
            return new HttpResponseMessage(Status);

        var path = request.RequestUri?.AbsolutePath ?? "";
        if (request.Method == HttpMethod.Get
            && path.Equals("/views/basket-contents", StringComparison.OrdinalIgnoreCase))
        {
            var buyerId = BuyerFromQuery(request.RequestUri?.Query);
            return Json(View(buyerId));
        }

        var json = request.Content is null
            ? ""
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (request.Method != HttpMethod.Post || string.IsNullOrWhiteSpace(json))
            return new HttpResponseMessage(HttpStatusCode.NotFound);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var buyer = root.GetProperty("buyerId").GetGuid();
        var productId = root.GetProperty("productId").GetGuid();

        if (path.Equals("/api/basket/add-item-to-basket", StringComparison.OrdinalIgnoreCase))
        {
            var name = root.GetProperty("productName").GetString() ?? "";
            var price = root.GetProperty("unitPrice").GetDecimal();
            string? picture = null;
            if (root.TryGetProperty("pictureUrl", out var pictureValue)
                && pictureValue.ValueKind == JsonValueKind.String)
            {
                picture = pictureValue.GetString();
            }

            lock (_gate)
            {
                var lines = Lines(buyer);
                var index = lines.FindIndex(line => line.ProductId == productId);
                if (index < 0)
                    lines.Add(new BasketLine(productId, name, price, 1, picture));
                else
                {
                    var line = lines[index];
                    lines[index] = line with { Quantity = line.Quantity + 1 };
                }
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        if (path.Equals("/api/basket/change-basket-item-quantity", StringComparison.OrdinalIgnoreCase))
        {
            var quantity = root.GetProperty("quantity").GetInt32();
            lock (_gate)
            {
                var lines = Lines(buyer);
                var index = lines.FindIndex(line => line.ProductId == productId);
                if (index >= 0 && quantity > 0)
                {
                    var line = lines[index];
                    lines[index] = line with { Quantity = quantity };
                }
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        if (path.Equals("/api/basket/remove-item-from-basket", StringComparison.OrdinalIgnoreCase))
        {
            lock (_gate)
                Lines(buyer).RemoveAll(line => line.ProductId == productId);

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    protected override void Dispose(bool disposing)
    {
    }

    private List<BasketLine> Lines(Guid buyerId)
    {
        if (!_lines.TryGetValue(buyerId, out var lines))
        {
            lines = [];
            _lines[buyerId] = lines;
        }

        return lines;
    }

    private object View(Guid buyerId)
    {
        List<BasketLine> lines;
        lock (_gate)
            lines = [.. Lines(buyerId)];

        var subtotal = 0m;
        var count = 0;
        var items = new List<object>(lines.Count);
        foreach (var line in lines)
        {
            subtotal += line.UnitPrice * line.Quantity;
            count += line.Quantity;
            items.Add(new
            {
                line.ProductId,
                line.ProductName,
                line.UnitPrice,
                line.Quantity,
                line.PictureUrl
            });
        }

        return new
        {
            BuyerId = buyerId,
            Items = items,
            ItemCount = count,
            Subtotal = subtotal,
            IsEmpty = items.Count == 0
        };
    }

    private static Guid BuyerFromQuery(string? query)
    {
        if (string.IsNullOrEmpty(query))
            return Guid.Empty;

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = part.Split('=', 2);
            if (split.Length == 2
                && split[0].Equals("buyerId", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(Uri.UnescapeDataString(split[1]), out var buyerId))
            {
                return buyerId;
            }
        }

        return Guid.Empty;
    }

    private static HttpResponseMessage Json(object body) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(body)
        };

    private sealed record BasketLine(
        Guid ProductId,
        string ProductName,
        decimal UnitPrice,
        int Quantity,
        string? PictureUrl);
}

public sealed class CatalogApiStub : HttpMessageHandler
{
    public static readonly Guid ItemId = Guid.Parse("e76d6631-839f-5c78-9369-f1fbb70fbb8f");

    public const string ItemName = "Alpine Tent";

    public const decimal ItemPrice = 199.00m;

    public const string PictureUrl = "/images/tent.png";

    private readonly List<string?> _authorization;

    public CatalogApiStub(List<string?> authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        _authorization = authorization;
    }

    private const string EmptyCatalog =
        """{"items":[],"totalItems":0,"pageIndex":0,"pageSize":10,"brandFacets":[],"typeFacets":[],"selectedBrandIds":[],"selectedTypeIds":[]}""";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        lock (_authorization)
            _authorization.Add(request.Headers.Authorization?.ToString());
        var path = request.RequestUri?.AbsolutePath ?? "";
        var prefix = "/views/view-catalog-item/";
        if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            if (Guid.TryParse(path[prefix.Length..], out var itemId) && itemId == ItemId)
            {
                var body = new
                {
                    ItemId,
                    Name = ItemName,
                    Description = "A two-person tent.",
                    Price = ItemPrice,
                    BrandName = "Alpine",
                    TypeName = "Camping",
                    AvailableStock = 5,
                    PictureUrl,
                    IsAvailable = true
                };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(body)
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(EmptyCatalog, Encoding.UTF8, "application/json")
        });
    }

    protected override void Dispose(bool disposing)
    {
    }
}

public sealed class StaticJsonHandler(string json, List<string?> authorizationHeaders) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        lock (authorizationHeaders)
            authorizationHeaders.Add(request.Headers.Authorization?.ToString());
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }

    protected override void Dispose(bool disposing)
    {
    }
}

public sealed class CollectingLoggerProvider : ILoggerProvider
{
    public List<string> Lines { get; } = [];

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, Lines);

    public void Dispose()
    {
    }

    private sealed class CollectingLogger(string category, List<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (lines)
                lines.Add(category + " " + formatter(state, exception));
        }
    }
}
