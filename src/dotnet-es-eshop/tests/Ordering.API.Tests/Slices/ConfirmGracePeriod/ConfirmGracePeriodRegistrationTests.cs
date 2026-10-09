using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Ordering.API.Slices.ConfirmGracePeriod;
using LawnDart.Samples.EShop.Ordering.API.Slices.PlaceOrder;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.ConfirmGracePeriod;

public class ConfirmGracePeriodRegistrationTests
{
    [Fact]
    public void Reactor_is_registered_once()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEShopReactors(typeof(ConfirmGracePeriodReactor).Assembly);

        var reactors = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType
                    == typeof(EShopReactorHost<ConfirmGracePeriodReactor, GracePeriodConfirmed>))
            .ToArray();

        Assert.Single(reactors);
    }

    [Fact]
    public void Shared_catalog_resolves_grace_period_confirmed()
    {
        var services = new ServiceCollection();
        services.AddInMemoryProjectionStores("default");
        var context = services.AddBoundedContext("default");
        context.AddOrderingContext(typeof(OrderPlaced).Assembly, typeof(GracePeriodConfirmed).Assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("grace-period-confirmed", out var confirmedType));
        Assert.Equal(typeof(GracePeriodConfirmed), confirmedType);
    }
}
