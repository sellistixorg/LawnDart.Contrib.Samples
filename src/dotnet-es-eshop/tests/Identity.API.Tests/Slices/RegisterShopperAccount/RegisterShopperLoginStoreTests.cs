using System.Text.Json;
using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Metadata;
using LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;
using LawnDart.Samples.EShop.Identity.Commands;
using LawnDart.Testing.Bdd;
using Microsoft.EntityFrameworkCore;

namespace LawnDart.Samples.EShop.Identity.API.Tests.Slices.RegisterShopperAccount;

public class RegisterShopperLoginStoreTests
{
    private const string Password = "Pass123$";

    [Fact]
    public async Task Register_stores_a_password_hash_and_not_the_plain_password()
    {
        await using var ctx = NewContext();
        await using var logins = await ShopperLoginTestHost.StartAsync();
        var accountId = Guid.NewGuid();
        var handler = new RegisterShopperAccountHandler(ctx.AggregateRepository, logins.Store);

        await handler.HandleAsync(Register(accountId, "ada", "ada@example.com"));

        var user = await logins.Users.FindByIdAsync(accountId.ToString());
        Assert.NotNull(user);
        Assert.Equal(accountId.ToString(), user.Id);
        Assert.Equal("ada", user.UserName);
        Assert.Equal("ada@example.com", user.Email);
        Assert.NotNull(user.PasswordHash);
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.DoesNotContain(Password, user.PasswordHash, StringComparison.Ordinal);
        Assert.True(await logins.Users.CheckPasswordAsync(user, Password));
        Assert.False(await logins.Users.CheckPasswordAsync(user, "wrong-password"));

        await AssertPasswordAbsentFromEvents(ctx, accountId, "ada@example.com");
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email()
    {
        await using var ctx = NewContext();
        await using var logins = await ShopperLoginTestHost.StartAsync();
        var handler = new RegisterShopperAccountHandler(ctx.AggregateRepository, logins.Store);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        await handler.HandleAsync(Register(firstId, "ada", "ada@example.com"));

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.HandleAsync(Register(secondId, "grace", "ADA@example.com")));

        Assert.Equal(ShopperAccount.EmailAlreadyRegisteredMessage, exception.Message);
        Assert.Equal(ShopperAccount.EmailAlreadyRegisteredTitle, exception.Title);

        var second = await ctx.AggregateRepository.GetOrCreateAsync<ShopperAccount>(secondId);
        Assert.False(second.State.Exists);
        Assert.Null(await logins.Users.FindByIdAsync(secondId.ToString()));
        Assert.NotNull(await logins.Users.FindByIdAsync(firstId.ToString()));
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_user_name()
    {
        await using var ctx = NewContext();
        await using var logins = await ShopperLoginTestHost.StartAsync();
        var handler = new RegisterShopperAccountHandler(ctx.AggregateRepository, logins.Store);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        await handler.HandleAsync(Register(firstId, "Ada", "ada@example.com"));

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.HandleAsync(Register(secondId, "ada", "grace@example.com")));

        Assert.Equal(ShopperLoginStore.UserNameAlreadyRegisteredMessage, exception.Message);
        Assert.Equal(ShopperLoginStore.UserNameAlreadyRegisteredTitle, exception.Title);

        var second = await ctx.AggregateRepository.GetOrCreateAsync<ShopperAccount>(secondId);
        Assert.False(second.State.Exists);
        var reserved = await ctx.AggregateRepository.GetOrCreateAsync<ShopperEmail>(
            ShopperEmail.EmailStreamId("grace@example.com"));
        Assert.False(reserved.State.Exists);
        Assert.Null(await logins.Users.FindByIdAsync(secondId.ToString()));

        var first = await logins.Users.FindByIdAsync(firstId.ToString());
        Assert.NotNull(first);
        Assert.Equal("Ada", first.UserName);
    }

    [Fact]
    public async Task Register_rejects_an_invalid_user_name_before_either_write()
    {
        await using var ctx = NewContext();
        await using var logins = await ShopperLoginTestHost.StartAsync();
        var accountId = Guid.NewGuid();
        var email = "ada.lovelace@example.com";
        var handler = new RegisterShopperAccountHandler(ctx.AggregateRepository, logins.Store);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.HandleAsync(Register(accountId, "ada lovelace", email)));

        Assert.Equal(ShopperLoginStore.UserNameInvalidMessage, exception.Message);
        Assert.Equal(ShopperLoginStore.UserNameInvalidTitle, exception.Title);

        var account = await ctx.AggregateRepository.GetOrCreateAsync<ShopperAccount>(accountId);
        Assert.False(account.State.Exists);
        Assert.Empty(await ctx.EventStore.ReadStreamAsync($"ShopperAccount:{accountId}"));

        var reserved = await ctx.AggregateRepository.GetOrCreateAsync<ShopperEmail>(
            ShopperEmail.EmailStreamId(email));
        Assert.False(reserved.State.Exists);
        Assert.Empty(await ctx.EventStore.ReadStreamAsync(ShopperEmail.EmailStreamId(email)));

        Assert.Null(await logins.Users.FindByIdAsync(accountId.ToString()));
        Assert.Empty(await logins.Db.Users.ToListAsync());
    }

    [Fact]
    public async Task Register_leaves_no_login_when_the_identity_insert_fails()
    {
        await using var ctx = NewContext();
        await using var logins = await ShopperLoginTestHost.StartAsync();
        var accountId = Guid.NewGuid();
        var handler = new RegisterShopperAccountHandler(
            ctx.AggregateRepository,
            new LoginInsertFails(logins.Store));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(Register(accountId, "ada", "ada@example.com")));

        Assert.Equal("login store unavailable", exception.Message);

        var account = await ctx.AggregateRepository.GetOrCreateAsync<ShopperAccount>(accountId);
        Assert.True(account.State.Exists);
        Assert.True(account.State.IsActive);
        Assert.Equal("ada@example.com", account.State.Email);

        var reserved = await ctx.AggregateRepository.GetOrCreateAsync<ShopperEmail>(
            ShopperEmail.EmailStreamId("ada@example.com"));
        Assert.True(reserved.State.Exists);
        Assert.Equal(accountId, reserved.State.AccountId);

        Assert.Empty(await logins.Db.Users.ToListAsync());
        await AssertPasswordAbsentFromEvents(ctx, accountId, "ada@example.com");
    }

    [Fact]
    public async Task Register_rejects_a_password_that_fails_identity_rules_before_either_write()
    {
        await using var ctx = NewContext();
        await using var logins = await ShopperLoginTestHost.StartAsync();
        var accountId = Guid.NewGuid();
        var handler = new RegisterShopperAccountHandler(ctx.AggregateRepository, logins.Store);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => handler.HandleAsync(new RegisterShopperAccountCommand(
                Guid.NewGuid(),
                accountId,
                "ada",
                "ada@example.com",
                "correct horse",
                "Ada",
                "Lovelace")));

        Assert.Equal(ShopperLoginStore.PasswordRequirementsNotMetTitle, exception.Title);
        Assert.DoesNotContain("correct horse", exception.Message, StringComparison.Ordinal);

        var account = await ctx.AggregateRepository.GetOrCreateAsync<ShopperAccount>(accountId);
        Assert.False(account.State.Exists);
        Assert.Empty(await logins.Db.Users.ToListAsync());
    }

    [Fact]
    public void Command_text_redacts_the_password()
    {
        var command = Register(Guid.NewGuid(), "ada", "ada@example.com");

        var text = command.ToString();

        Assert.Contains("[redacted]", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, text, StringComparison.Ordinal);
        Assert.Contains("ada@example.com", text, StringComparison.Ordinal);
    }

    private static BddTestContext NewContext() =>
        BddTestContext.CreateInMemory(typeof(ShopperAccountRegistered), typeof(ShopperEmailReserved));

    private static RegisterShopperAccountCommand Register(Guid accountId, string userName, string email) =>
        new(Guid.NewGuid(), accountId, userName, email, Password, "Ada", "Lovelace");

    private static async Task AssertPasswordAbsentFromEvents(
        BddTestContext ctx,
        Guid accountId,
        string email)
    {
        foreach (var streamId in new[]
        {
            $"ShopperAccount:{accountId}",
            ShopperEmail.EmailStreamId(email)
        })
        {
            var events = await ctx.EventStore.ReadStreamAsync(streamId);
            Assert.NotEmpty(events);
            foreach (var sequenced in events)
            {
                var json = JsonSerializer.Serialize(new StoredEvent(sequenced.Event, sequenced.Metadata));
                Assert.DoesNotContain(Password, json, StringComparison.Ordinal);
            }
        }
    }

    private sealed record StoredEvent(IEvent Event, EventMetadata Metadata);

    private sealed class LoginInsertFails : IShopperLoginStore
    {
        private readonly IShopperLoginStore _inner;

        public LoginInsertFails(IShopperLoginStore inner) => _inner = inner;

        public Task ValidateNewLoginAsync(
            string userName,
            string email,
            string password,
            CancellationToken cancellationToken = default) =>
            _inner.ValidateNewLoginAsync(userName, email, password, cancellationToken);

        public Task CreateAsync(
            Guid accountId,
            string userName,
            string email,
            string password,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("login store unavailable");

        public Task ConfirmEmailAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            _inner.ConfirmEmailAsync(accountId, cancellationToken);

        public Task SetPaymentCardAsync(
            Guid accountId,
            ShopperPaymentCard card,
            CancellationToken cancellationToken = default) =>
            _inner.SetPaymentCardAsync(accountId, card, cancellationToken);

        public Task<Guid?> CheckUserNamePasswordAsync(
            string userName,
            string password,
            CancellationToken cancellationToken = default) =>
            _inner.CheckUserNamePasswordAsync(userName, password, cancellationToken);
    }
}
