using LawnDart;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Identity.API.Slices.SignInShopper;
using LawnDart.Samples.EShop.Identity.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Identity.API.Tests.Slices.SignInShopper;

public class SignInShopperRegistrationTests
{
    [Fact]
    public void Identity_assembly_scan_registers_sign_in_shopper()
    {
        var services = new ServiceCollection();
        var context = services.AddBoundedContext("default");
        var assembly = typeof(ShopperSignedIn).Assembly;
        var signedIn = assembly.GetTypes()
            .Where(type => EventTypeCatalog.TryGetDeclaredName(type) == "shopper-signed-in")
            .ToArray();

        Assert.Single(signedIn);
        Assert.Equal(typeof(ShopperSignedIn), signedIn[0]);

        context.AddIdentityContext(assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("shopper-signed-in", out var eventType));
        Assert.Equal(typeof(ShopperSignedIn), eventType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<SignInShopperCommand>));
    }
}
