using LawnDart;
using LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;
using LawnDart.Samples.EShop.Identity.Commands;
using LawnDart.Testing.Bdd;

namespace LawnDart.Samples.EShop.Identity.API.Tests.Slices.RegisterShopperAccount;

public class RegisterShopperAccountTests
{
    [Fact]
    public async Task Register_emits_shopper_account_registered()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(ShopperAccountRegistered));
        var accountId = Guid.NewGuid();

        await AggregateSpec
            .For<ShopperAccount>(ctx, accountId)
            .When(new RegisterShopperAccountCommand(
                Guid.NewGuid(),
                accountId,
                "ada",
                "ada@example.com",
                "correct horse",
                "Ada",
                "Lovelace"))
            .ThenEmittedEvent<ShopperAccountRegistered>(e =>
                e.AccountId == accountId
                && e.UserName == "ada"
                && e.Email == "ada@example.com"
                && e.Name == "Ada"
                && e.LastName == "Lovelace")
            .ThenAppendedOnlyTo($"ShopperAccount:{accountId}")
            .AndAssert(result =>
            {
                var account = Assert.IsType<ShopperAccount>(result.Aggregate);
                Assert.True(account.State.Exists);
                Assert.True(account.State.IsActive);
                Assert.Equal("ada@example.com", account.State.Email);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Register_rejects_an_account_that_already_has_the_email()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(ShopperAccountRegistered));
        var accountId = Guid.NewGuid();

        await AggregateSpec
            .For<ShopperAccount>(ctx, accountId)
            .Given(new ShopperAccountRegistered(
                Guid.NewGuid(),
                DateTime.UtcNow,
                accountId,
                "ada",
                "ada@example.com",
                "Ada",
                "Lovelace"))
            .When(new RegisterShopperAccountCommand(
                Guid.NewGuid(),
                accountId,
                "ada",
                "ada@example.com",
                "correct horse",
                "Ada",
                "Lovelace"))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var exception = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(ShopperAccount.EmailAlreadyRegisteredMessage, exception.Message);
                Assert.Equal(ShopperAccount.EmailAlreadyRegisteredTitle, exception.Title);
            })
            .RunAsync();
    }

    [Fact]
    public async Task Register_rejects_the_same_email_in_different_case_for_a_new_account()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(ShopperAccountRegistered),
            typeof(ShopperEmailReserved));
        await using var logins = await ShopperLoginTestHost.StartAsync();
        var handler = new RegisterShopperAccountHandler(ctx.AggregateRepository, logins.Store);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        await handler.HandleAsync(Register(firstId, "ada@example.com"));

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.HandleAsync(Register(secondId, " Ada@Example.com ", "grace")));

        Assert.Equal(ShopperAccount.EmailAlreadyRegisteredMessage, exception.Message);
        Assert.Equal(ShopperAccount.EmailAlreadyRegisteredTitle, exception.Title);

        var reserved = await ctx.AggregateRepository.GetOrCreateAsync<ShopperEmail>(
            ShopperEmail.EmailStreamId("Ada@Example.com"));
        Assert.True(reserved.State.Exists);
        Assert.Equal("ada@example.com", reserved.State.Email);
        Assert.Equal(firstId, reserved.State.AccountId);

        var second = await ctx.AggregateRepository.GetOrCreateAsync<ShopperAccount>(secondId);
        Assert.False(second.State.Exists);
    }

    [Fact]
    public async Task Register_accepts_a_different_email()
    {
        await using var ctx = BddTestContext.CreateInMemory(
            typeof(ShopperAccountRegistered),
            typeof(ShopperEmailReserved));
        await using var logins = await ShopperLoginTestHost.StartAsync();
        var handler = new RegisterShopperAccountHandler(ctx.AggregateRepository, logins.Store);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        await handler.HandleAsync(Register(firstId, "ada@example.com"));
        await handler.HandleAsync(Register(secondId, "Grace@Example.com", "grace"));

        var second = await ctx.AggregateRepository.GetOrCreateAsync<ShopperAccount>(secondId);
        Assert.True(second.State.Exists);
        Assert.True(second.State.IsActive);
        Assert.Equal("Grace@Example.com", second.State.Email);

        var reserved = await ctx.AggregateRepository.GetOrCreateAsync<ShopperEmail>(
            ShopperEmail.EmailStreamId("grace@example.com"));
        Assert.True(reserved.State.Exists);
        Assert.Equal(secondId, reserved.State.AccountId);
    }

    private static RegisterShopperAccountCommand Register(
        Guid accountId,
        string email,
        string userName = "ada") =>
        new(
            Guid.NewGuid(),
            accountId,
            userName,
            email,
            "Pass123$",
            "Ada",
            "Lovelace");

    [Fact]
    public async Task Register_rejects_an_empty_account_id()
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(ShopperAccountRegistered));

        await AggregateSpec
            .For<ShopperAccount>(ctx, Guid.NewGuid())
            .When(new RegisterShopperAccountCommand(
                Guid.NewGuid(),
                Guid.Empty,
                "ada",
                "ada@example.com",
                "correct horse",
                "Ada",
                "Lovelace"))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal("Account id is required.", result.Exception!.Message))
            .RunAsync();
    }

    [Theory]
    [InlineData("  ", "ada@example.com", "correct horse", "Ada", "Lovelace", "User name is required.")]
    [InlineData("ada", "  ", "correct horse", "Ada", "Lovelace", "Email is required.")]
    [InlineData("ada", "ada@example.com", "  ", "Ada", "Lovelace", "Password is required.")]
    [InlineData("ada", "ada@example.com", "correct horse", "  ", "Lovelace", "Name is required.")]
    [InlineData("ada", "ada@example.com", "correct horse", "Ada", "  ", "Last name is required.")]
    public async Task Register_rejects_a_blank_required_field(
        string userName,
        string email,
        string password,
        string name,
        string lastName,
        string message)
    {
        await using var ctx = BddTestContext.CreateInMemory(typeof(ShopperAccountRegistered));
        var accountId = Guid.NewGuid();

        await AggregateSpec
            .For<ShopperAccount>(ctx, accountId)
            .When(new RegisterShopperAccountCommand(
                Guid.NewGuid(),
                accountId,
                userName,
                email,
                password,
                name,
                lastName))
            .ThenThrows<DomainException>()
            .AndAssert(result => Assert.Equal(message, result.Exception!.Message))
            .RunAsync();
    }
}
