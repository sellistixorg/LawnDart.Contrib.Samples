using System.Net;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.WebApp.Models;
using LawnDart.Samples.EShop.WebApp.Placeholders;
using LawnDart.Samples.EShop.WebApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.WebApp.Tests;

public class CheckoutClientTests
{
    [Fact(Timeout = 30000)]
    public async Task Place_order_waits_until_the_bag_is_empty_then_notifies()
    {
        await using var harness = CheckoutHarness.Start();
        harness.BasketStub.Seed(
            IdentityStub.AliceId,
            CatalogApiStub.ItemId,
            CatalogApiStub.ItemName,
            CatalogApiStub.ItemPrice,
            3,
            CatalogApiStub.PictureUrl);
        harness.Ordering.ClearBasketDelay = TimeSpan.FromMilliseconds(250);

        var calls = 0;
        var emptyOnNotify = false;
        harness.Basket.NotifyOnChange(async () =>
        {
            calls++;
            var items = await harness.Basket.GetBasketItemsAsync();
            emptyOnNotify = items.Count == 0;
        });

        var waiting = harness.Checkout.PlaceOrderAsync(SampleAddress());
        await Task.Delay(100);
        Assert.False(waiting.IsCompleted);
        Assert.Equal(3, harness.BasketStub.QuantityOf(IdentityStub.AliceId, CatalogApiStub.ItemId));

        await waiting;

        Assert.True(emptyOnNotify);
        Assert.True(calls >= 1);
        Assert.Empty(await harness.Basket.GetBasketItemsAsync());
        Assert.Equal(1, harness.Ordering.PlaceOrderCount);
    }

    [Fact(Timeout = 30000)]
    public async Task Place_order_rejection_does_not_wait_for_an_empty_bag()
    {
        await using var harness = CheckoutHarness.Start();
        harness.BasketStub.Seed(
            IdentityStub.AliceId,
            CatalogApiStub.ItemId,
            CatalogApiStub.ItemName,
            CatalogApiStub.ItemPrice,
            2,
            CatalogApiStub.PictureUrl);
        harness.Ordering.PlaceOrderStatus = HttpStatusCode.UnprocessableEntity;
        harness.Ordering.RejectionTitle = "ORDER_ITEMS_EMPTY";
        harness.Ordering.ClearBasketDelay = TimeSpan.FromSeconds(10);

        var calls = 0;
        harness.Basket.NotifyOnChange(() =>
        {
            calls++;
            return Task.CompletedTask;
        });

        var error = await Assert.ThrowsAsync<OrderingCommandRejectedException>(
            () => harness.Checkout.PlaceOrderAsync(SampleAddress()));

        Assert.Equal("Your cart is empty.", error.Message);
        Assert.Equal(0, calls);
        Assert.Equal(2, harness.BasketStub.QuantityOf(IdentityStub.AliceId, CatalogApiStub.ItemId));
    }

    private static BasketCheckoutInfo SampleAddress() => new()
    {
        RequestId = Guid.NewGuid(),
        Street = "123 Main",
        City = "Seattle",
        State = "WA",
        Country = "USA",
        ZipCode = "98101"
    };

    private sealed class CheckoutHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        private CheckoutHarness(
            ServiceProvider provider,
            AsyncServiceScope scope,
            BasketApiStub basketStub,
            OrderingApiStub ordering)
        {
            _provider = provider;
            Scope = scope;
            BasketStub = basketStub;
            Ordering = ordering;
            Basket = scope.ServiceProvider.GetRequiredService<IBasketContents>();
            Checkout = scope.ServiceProvider.GetRequiredService<ICheckout>();
        }

        public AsyncServiceScope Scope { get; }

        public BasketApiStub BasketStub { get; }

        public OrderingApiStub Ordering { get; }

        public IBasketContents Basket { get; }

        public ICheckout Checkout { get; }

        public static CheckoutHarness Start()
        {
            var basketStub = new BasketApiStub();
            var authorization = new List<string?>();
            var ordering = new OrderingApiStub(authorization) { Basket = basketStub };
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
            services.AddHttpContextAccessor();
            services.AddHttpClient(EShopStorefrontClients.Basket, client => client.BaseAddress = new Uri("http://basket.test/"))
                .ConfigurePrimaryHttpMessageHandler(() => basketStub);
            services.AddHttpClient(EShopStorefrontClients.Ordering, client => client.BaseAddress = new Uri("http://ordering.test/"))
                .ConfigurePrimaryHttpMessageHandler(() => ordering);
            services.AddHttpClient(EShopStorefrontClients.Identity, client => client.BaseAddress = new Uri("http://identity.test/"))
                .ConfigurePrimaryHttpMessageHandler(() => new IdentityStub());
            services.AddEShopWebAppPlaceholders();
            var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var scope = provider.CreateAsyncScope();
            var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext
            {
                User = ShopperSession.CreatePrincipal(
                    IdentityStub.AliceId,
                    "Alice",
                    IdentityStub.AccessToken,
                    DateTimeOffset.UtcNow.AddHours(1))
            };
            return new CheckoutHarness(provider, scope, basketStub, ordering);
        }

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
