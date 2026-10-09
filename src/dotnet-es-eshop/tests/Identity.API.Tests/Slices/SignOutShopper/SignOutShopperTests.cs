using System.Text.Json;
using LawnDart;
using LawnDart.Metadata;
using LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Slices.SignInShopper;
using LawnDart.Samples.EShop.Identity.API.Slices.SignOutShopper;
using LawnDart.Samples.EShop.Identity.Commands;
using LawnDart.Testing.Bdd;

namespace LawnDart.Samples.EShop.Identity.API.Tests.Slices.SignOutShopper;

public class SignOutShopperTests
{
    [Fact]
    public async Task Sign_out_appends_shopper_signed_out()
    {
        await using var ctx = NewContext();
        var accountId = Guid.NewGuid();
        const string logoutId = "logout-context-1";

        await AggregateSpec
            .For<ShopperAccount>(ctx, accountId)
            .Given(Registered(accountId))
            .Given(SignedIn(accountId))
            .When(SignOut(accountId, logoutId))
            .ThenEmittedTypes(typeof(ShopperSignedOut))
            .ThenEmittedEvent<ShopperSignedOut>(e => e.AccountId == accountId)
            .ThenAppendedOnlyTo($"ShopperAccount:{accountId}")
            .AndAssert(result =>
            {
                var account = Assert.IsType<ShopperAccount>(result.Aggregate);
                Assert.Equal(accountId, account.State.AccountId);
                Assert.Equal("ada", account.State.UserName);
                Assert.True(account.State.Exists);
                Assert.True(account.State.IsActive);
            })
            .RunAsync();

        await AssertLogoutIdAbsent(ctx, accountId, logoutId);
    }

    [Fact]
    public async Task Sign_out_rejects_an_unknown_account()
    {
        await using var ctx = NewContext();
        var accountId = Guid.NewGuid();

        await AggregateSpec
            .For<ShopperAccount>(ctx, accountId)
            .When(SignOut(accountId, "logout-context-1"))
            .ThenThrows<DomainException>()
            .AndAssert(result =>
            {
                var exception = Assert.IsType<DomainException>(result.Exception);
                Assert.Equal(ShopperAccount.NotFoundMessage, exception.Message);
                Assert.Equal(ShopperAccount.NotFoundTitle, exception.Title);
            })
            .RunAsync();

        var events = await ctx.EventStore.ReadStreamAsync($"ShopperAccount:{accountId}");
        Assert.Empty(events);
    }

    private static BddTestContext NewContext() =>
        BddTestContext.CreateInMemory(
            typeof(ShopperAccountRegistered),
            typeof(ShopperSignedIn),
            typeof(ShopperSignedOut));

    private static ShopperAccountRegistered Registered(Guid accountId) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            accountId,
            "ada",
            "ada@example.com",
            "Ada",
            "Lovelace");

    private static ShopperSignedIn SignedIn(Guid accountId) =>
        new(Guid.NewGuid(), DateTime.UtcNow, accountId, "ada");

    private static SignOutShopperCommand SignOut(Guid accountId, string? logoutId) =>
        new(Guid.NewGuid(), accountId, logoutId);

    private static async Task AssertLogoutIdAbsent(BddTestContext ctx, Guid accountId, string logoutId)
    {
        var events = await ctx.EventStore.ReadStreamAsync($"ShopperAccount:{accountId}");
        Assert.Contains(events, sequenced => sequenced.Event is ShopperSignedOut);
        foreach (var sequenced in events)
        {
            var json = JsonSerializer.Serialize(new StoredEvent(sequenced.Event, sequenced.Metadata));
            Assert.DoesNotContain(logoutId, json, StringComparison.Ordinal);
        }
    }

    private sealed record StoredEvent(IEvent Event, EventMetadata Metadata);
}
