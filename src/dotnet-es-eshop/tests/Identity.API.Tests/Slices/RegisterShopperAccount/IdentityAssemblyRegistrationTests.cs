using LawnDart;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;
using LawnDart.Samples.EShop.Identity.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Identity.API.Tests.Slices.RegisterShopperAccount;

public class IdentityAssemblyRegistrationTests
{
    [Fact]
    public void Identity_assembly_scan_registers_register_shopper_account_once()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext("default");
        var assembly = typeof(ShopperAccountRegistered).Assembly;

        context.AddIdentityContext(assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("shopper-account-registered", out var eventType));
        Assert.Equal(typeof(ShopperAccountRegistered), eventType);
        Assert.True(context.EventCatalog.TryResolveType("shopper-email-reserved", out var emailEventType));
        Assert.Equal(typeof(ShopperEmailReserved), emailEventType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<RegisterShopperAccountCommand>));

        var ex = Assert.Throws<InvalidOperationException>(() => context.AddIdentityContext(assembly));
        Assert.Contains("once", ex.Message, StringComparison.Ordinal);
    }
}
