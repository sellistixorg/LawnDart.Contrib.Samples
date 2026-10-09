using LawnDart;
using LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Slices.ShopperAccountProfile;
using LawnDart.Samples.EShop.Identity.API.Slices.UpdateShopperProfile;
using LawnDart.Samples.EShop.Identity.Commands;
using LawnDart.Testing.Bdd;

namespace LawnDart.Samples.EShop.Identity.API.Tests.Slices.UpdateShopperProfile;

public class UpdateShopperProfileTests
{
    [Fact]
    public async Task Update_emits_shopper_profile_updated()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(ShopperAccountRegistered),
            typeof(ShopperProfileUpdated));
        var accountId = Guid.NewGuid();

        await AggregateSpec
            .For<ShopperAccount>(ctx, accountId)
            .Given(Registered(accountId))
            .When(Update(accountId, "  Augusta  ", "  King  ", "  555-0100  ", "  1 Main  ", "  Redmond  ", "  WA  ", "  USA  ", "  98052  "))
            .ThenEmittedTypes(typeof(ShopperProfileUpdated))
            .ThenEmittedEvent<ShopperProfileUpdated>(e =>
                e.AccountId == accountId
                && e.Name == "Augusta"
                && e.LastName == "King"
                && e.PhoneNumber == "555-0100"
                && e.Street == "1 Main"
                && e.City == "Redmond"
                && e.State == "WA"
                && e.Country == "USA"
                && e.ZipCode == "98052")
            .ThenAppendedOnlyTo($"ShopperAccount:{accountId}")
            .AndAssert(result =>
            {
                var account = Assert.IsType<ShopperAccount>(result.Aggregate);
                Assert.Equal("Augusta", account.State.Name);
                Assert.Equal("King", account.State.LastName);
                Assert.Equal("555-0100", account.State.PhoneNumber);
                Assert.Equal("1 Main", account.State.Street);
                Assert.Equal("Redmond", account.State.City);
                Assert.Equal("WA", account.State.State);
                Assert.Equal("USA", account.State.Country);
                Assert.Equal("98052", account.State.ZipCode);
                Assert.Equal("ada", account.State.UserName);
                Assert.Equal("ada@example.com", account.State.Email);
                Assert.True(account.State.IsActive);
                Assert.True(account.State.Exists);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Update_clears_optional_fields_when_they_are_blank()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(ShopperAccountRegistered),
            typeof(ShopperProfileUpdated));
        var accountId = Guid.NewGuid();

        await AggregateSpec
            .For<ShopperAccount>(ctx, accountId)
            .Given(Registered(accountId))
            .Given(new ShopperProfileUpdated(
                Guid.NewGuid(),
                DateTime.UtcNow,
                accountId,
                "Ada",
                "Lovelace",
                "555-0100",
                "1 Main",
                "Redmond",
                "WA",
                "USA",
                "98052"))
            .When(Update(accountId, "Ada", "Lovelace", " ", " ", " ", " ", " ", " "))
            .ThenEmittedEvent<ShopperProfileUpdated>(e =>
                e.Name == "Ada"
                && e.LastName == "Lovelace"
                && e.PhoneNumber == null
                && e.Street == null
                && e.City == null
                && e.State == null
                && e.Country == null
                && e.ZipCode == null)
            .AndAssert(result =>
            {
                var account = Assert.IsType<ShopperAccount>(result.Aggregate);
                Assert.Null(account.State.PhoneNumber);
                Assert.Null(account.State.Street);
                Assert.Null(account.State.City);
                Assert.Null(account.State.State);
                Assert.Null(account.State.Country);
                Assert.Null(account.State.ZipCode);
                Assert.Equal("ada@example.com", account.State.Email);
            })
            .RunAsync();
    }

    [Fact]
    public void Profile_updated_has_no_card_or_security_number()
    {
        AssertNoCardOrSecurityNumber(typeof(ShopperProfileUpdated));
        AssertNoCardOrSecurityNumber(typeof(UpdateShopperProfileCommand));
        AssertNoCardOrSecurityNumber(typeof(ShopperAccountState));
        AssertNoCardOrSecurityNumber(typeof(ShopperAccountProfileView));
        AssertNoCardOrSecurityNumber(typeof(LawnDart.Samples.EShop.WebApp.Models.ShopperAccountProfileView));
    }

    private static void AssertNoCardOrSecurityNumber(Type type)
    {
        var names = type.GetProperties().Select(property => property.Name);
        Assert.DoesNotContain(names, name =>
            name.Contains("Card", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Security", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Update_rejects_an_unknown_account()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(ShopperAccountRegistered),
            typeof(ShopperProfileUpdated));
        var accountId = Guid.NewGuid();

        await AggregateSpec
            .For<ShopperAccount>(ctx, accountId)
            .When(Update(accountId, "Ada", "Lovelace"))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var exception = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(ShopperAccount.NotFoundMessage, exception.Message);
                Assert.Equal(ShopperAccount.NotFoundTitle, exception.Title);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Update_rejects_an_empty_account_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(ShopperAccountRegistered),
            typeof(ShopperProfileUpdated));

        await AggregateSpec
            .For<ShopperAccount>(ctx, Guid.NewGuid())
            .When(Update(Guid.Empty, "Ada", "Lovelace"))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Account id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Theory]
    [InlineData("  ", "Lovelace", "Name is required.")]
    [InlineData("Ada", "  ", "Last name is required.")]
    public async Task Update_rejects_a_blank_required_name(string name, string lastName, string message)
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(ShopperAccountRegistered),
            typeof(ShopperProfileUpdated));
        var accountId = Guid.NewGuid();

        await AggregateSpec
            .For<ShopperAccount>(ctx, accountId)
            .Given(Registered(accountId))
            .When(Update(accountId, name, lastName))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal(message, result.Exception!.Message))
            .RunAsync();
    }

    private static ShopperAccountRegistered Registered(Guid accountId) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            accountId,
            "ada",
            "ada@example.com",
            "Ada",
            "Lovelace");

    private static UpdateShopperProfileCommand Update(
        Guid accountId,
        string name,
        string lastName,
        string? phoneNumber = null,
        string? street = null,
        string? city = null,
        string? state = null,
        string? country = null,
        string? zipCode = null) =>
        new(
            Guid.NewGuid(),
            accountId,
            name,
            lastName,
            phoneNumber,
            street,
            city,
            state,
            country,
            zipCode);
}
