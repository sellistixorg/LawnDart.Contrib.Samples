using LawnDart;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Slices.SignInShopper;
using LawnDart.Samples.EShop.Identity.API.Slices.UpdateShopperProfile;
using LawnDart.Samples.EShop.Identity.Commands;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.Identity.API.Tests.Slices.UpdateShopperProfile;

public class UpdateShopperProfileRegistrationTests
{
    [Fact]
    public void Each_identity_event_type_name_is_declared_exactly_once()
    {
        var assembly = typeof(ShopperProfileUpdated).Assembly;
        var declared = assembly.GetTypes()
            .Select(type => new { Type = type, Name = EventTypeCatalog.TryGetDeclaredName(type) })
            .Where(entry => entry.Name is not null)
            .ToArray();

        Assert.NotEmpty(declared);
        var duplicates = declared
            .GroupBy(entry => entry.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(entry => entry.Type.FullName))}")
            .ToArray();
        Assert.Empty(duplicates);

        var services = new ServiceCollection();
        var context = services.AddBoundedContext("default");
        context.AddIdentityContext(assembly);

        Assert.NotNull(context.EventCatalog);
        Assert.True(context.EventCatalog.TryResolveType("shopper-profile-updated", out var updatedType));
        Assert.Equal(typeof(ShopperProfileUpdated), updatedType);
        Assert.True(context.EventCatalog.TryResolveType("shopper-account-registered", out var registeredType));
        Assert.Equal(typeof(ShopperAccountRegistered), registeredType);
        Assert.True(context.EventCatalog.TryResolveType("shopper-email-reserved", out var reservedType));
        Assert.Equal(typeof(ShopperEmailReserved), reservedType);
        Assert.True(context.EventCatalog.TryResolveType("shopper-signed-in", out var signedInType));
        Assert.Equal(typeof(ShopperSignedIn), signedInType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<UpdateShopperProfileCommand>));
    }
}
