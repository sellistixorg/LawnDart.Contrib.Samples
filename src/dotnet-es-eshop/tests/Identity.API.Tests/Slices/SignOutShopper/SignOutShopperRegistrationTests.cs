using LawnDart;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Identity.API.Slices.SignOutShopper;
using LawnDart.Samples.EShop.Identity.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Identity.API.Tests.Slices.SignOutShopper;

public class SignOutShopperRegistrationTests
{
    [Fact]
    public void Identity_assembly_scan_registers_sign_out_shopper()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext("default");
        var assembly = typeof(ShopperSignedOut).Assembly;
        var signedOut = assembly.GetTypes()
            .Where(type => EventTypeCatalog.TryGetDeclaredName(type) == "shopper-signed-out")
            .ToArray();

        Assert.Single(signedOut);
        Assert.Equal(typeof(ShopperSignedOut), signedOut[0]);

        context.AddIdentityContext(assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("shopper-signed-out", out var eventType));
        Assert.Equal(typeof(ShopperSignedOut), eventType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<SignOutShopperCommand>));
    }
}
