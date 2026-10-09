using System.Text.Json;
using LawnDart;
using LawnDart.Metadata;
using LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;
using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Identity.API.Slices.SignInShopper;
using LawnDart.Samples.EShop.Identity.API.Tests.Slices.RegisterShopperAccount;
using LawnDart.Samples.EShop.Identity.Commands;
using LawnDart.Testing.Bdd;

namespace LawnDart.Samples.EShop.Identity.API.Tests.Slices.SignInShopper;

public class SignInShopperTests
{
    private const string Password = "Pass123$";
    private const string WrongPassword = "Wrong123$";

    [Fact]
    public async Task Sign_in_appends_shopper_signed_in()
    {
        await using var ctx = NewContext();
        await using var logins = await ShopperLoginTestHost.StartAsync();
        var accountId = Guid.NewGuid();
        await Register(ctx, logins, accountId, "ada");
        var receipt = new SignInShopperReceipt();
        var handler = Handler(ctx, logins, receipt);
        var returnUrl = "https://shop.example/return";

        await handler.HandleAsync(SignIn("ada", Password, returnUrl, rememberLogin: true));

        var events = await ctx.EventStore.ReadStreamAsync($"ShopperAccount:{accountId}");
        var signedIn = Assert.IsType<ShopperSignedIn>(events[^1].Event);
        Assert.Equal(accountId, signedIn.AccountId);
        Assert.Equal("ada", signedIn.UserName);
        Assert.NotNull(receipt.Result);
        Assert.Equal(accountId, receipt.Result.AccountId);
        Assert.Equal("Ada Lovelace", receipt.Result.DisplayName);
        Assert.Equal(FixedIssuer.Token, receipt.Result.AccessToken);
        Assert.Equal(FixedIssuer.ExpiresAt, receipt.Result.AccessTokenExpiresAt);
        Assert.DoesNotContain(Password, receipt.Result.DisplayName, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, receipt.Result.AccessToken, StringComparison.Ordinal);
        Assert.Equal(2, events.Count);

        var account = await ctx.AggregateRepository.GetOrCreateAsync<ShopperAccount>(accountId);
        Assert.True(account.State.Exists);
        Assert.True(account.State.IsActive);
        Assert.Equal("ada", account.State.UserName);

        await AssertSecretAbsent(ctx, accountId, Password, returnUrl);
        Assert.DoesNotContain(logins.Logs, line => line.Contains(Password, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Wrong_password_and_unknown_user_name_share_one_error()
    {
        await using var ctx = NewContext();
        await using var logins = await ShopperLoginTestHost.StartAsync();
        var accountId = Guid.NewGuid();
        await Register(ctx, logins, accountId, "ada");
        var handler = Handler(ctx, logins);

        var wrongPassword = await Assert.ThrowsAsync<DomainException>(
            () => handler.HandleAsync(SignIn("ada", WrongPassword)));
        var unknownUser = await Assert.ThrowsAsync<DomainException>(
            () => handler.HandleAsync(SignIn("nobody", Password)));

        Assert.Equal(ShopperAccount.InvalidCredentialsMessage, wrongPassword.Message);
        Assert.Equal("INVALID_CREDENTIALS", ShopperAccount.InvalidCredentialsTitle);
        Assert.Equal(ShopperAccount.InvalidCredentialsTitle, wrongPassword.Title);
        Assert.Equal(wrongPassword.Message, unknownUser.Message);
        Assert.Equal(wrongPassword.Title, unknownUser.Title);
        Assert.DoesNotContain(Password, wrongPassword.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(WrongPassword, wrongPassword.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, unknownUser.Message, StringComparison.Ordinal);

        var events = await ctx.EventStore.ReadStreamAsync($"ShopperAccount:{accountId}");
        Assert.DoesNotContain(events, sequenced => sequenced.Event is ShopperSignedIn);
        Assert.DoesNotContain(logins.Logs, line =>
            line.Contains(Password, StringComparison.Ordinal)
            || line.Contains(WrongPassword, StringComparison.Ordinal));

        var user = await logins.Users.FindByNameAsync("ada");
        Assert.NotNull(user);
        Assert.Equal(1, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task Email_address_is_not_a_user_name()
    {
        await using var ctx = NewContext();
        await using var logins = await ShopperLoginTestHost.StartAsync();
        var accountId = Guid.NewGuid();
        await Register(ctx, logins, accountId, "alice");
        var receipt = new SignInShopperReceipt();
        var handler = Handler(ctx, logins, receipt);

        var failure = await Assert.ThrowsAsync<DomainException>(
            () => handler.HandleAsync(SignIn("alice@example.com", Password)));

        Assert.Equal(ShopperAccount.InvalidCredentialsMessage, failure.Message);
        Assert.Equal(ShopperAccount.InvalidCredentialsTitle, failure.Title);
        Assert.Null(receipt.Result);
        Assert.DoesNotContain(Password, failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("alice@example.com", failure.Message, StringComparison.Ordinal);

        var events = await ctx.EventStore.ReadStreamAsync($"ShopperAccount:{accountId}");
        Assert.DoesNotContain(events, sequenced => sequenced.Event is ShopperSignedIn);
        Assert.DoesNotContain(logins.Logs, line =>
            line.Contains(Password, StringComparison.Ordinal)
            || line.Contains("alice@example.com", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Lockout_uses_the_same_error_and_appends_no_event()
    {
        await using var ctx = NewContext();
        await using var logins = await ShopperLoginTestHost.StartAsync();
        var accountId = Guid.NewGuid();
        await Register(ctx, logins, accountId, "ada");
        var handler = Handler(ctx, logins);
        var attempts = logins.Users.Options.Lockout.MaxFailedAccessAttempts;

        DomainException? first = null;
        for (var i = 0; i < attempts; i++)
        {
            var failure = await Assert.ThrowsAsync<DomainException>(
                () => handler.HandleAsync(SignIn("ada", WrongPassword)));
            first ??= failure;
        }

        var locked = await Assert.ThrowsAsync<DomainException>(
            () => handler.HandleAsync(SignIn("ada", Password)));

        Assert.NotNull(first);
        Assert.Equal(first.Message, locked.Message);
        Assert.Equal(ShopperAccount.InvalidCredentialsTitle, locked.Title);
        Assert.DoesNotContain(Password, locked.Message, StringComparison.Ordinal);

        var user = await logins.Users.FindByNameAsync("ada");
        Assert.NotNull(user);
        Assert.NotNull(user.LockoutEnd);
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);

        var events = await ctx.EventStore.ReadStreamAsync($"ShopperAccount:{accountId}");
        Assert.DoesNotContain(events, sequenced => sequenced.Event is ShopperSignedIn);
        Assert.DoesNotContain(logins.Logs, line =>
            line.Contains(Password, StringComparison.Ordinal)
            || line.Contains(WrongPassword, StringComparison.Ordinal));
    }

    [Fact]
    public void Command_text_redacts_the_password()
    {
        var command = SignIn("ada", Password, "/after");

        var text = command.ToString();

        Assert.Contains("[redacted]", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, text, StringComparison.Ordinal);
        Assert.Contains("ada", text, StringComparison.Ordinal);
    }

    private static BddTestContext NewContext() =>
        BddTestContext.CreateInMemory(
            typeof(ShopperAccountRegistered),
            typeof(ShopperEmailReserved),
            typeof(ShopperSignedIn));

    private static SignInShopperHandler Handler(
        BddTestContext ctx,
        ShopperLoginTestHost logins,
        SignInShopperReceipt? receipt = null) =>
        new(ctx.AggregateRepository, logins.Store, receipt ?? new SignInShopperReceipt(), new FixedIssuer());

    private static async Task Register(
        BddTestContext ctx,
        ShopperLoginTestHost logins,
        Guid accountId,
        string userName)
    {
        var handler = new RegisterShopperAccountHandler(ctx.AggregateRepository, logins.Store);
        await handler.HandleAsync(new RegisterShopperAccountCommand(
            Guid.NewGuid(),
            accountId,
            userName,
            $"{userName}@example.com",
            Password,
            "Ada",
            "Lovelace"));
    }

    private static SignInShopperCommand SignIn(
        string userName,
        string password,
        string? returnUrl = null,
        bool rememberLogin = false) =>
        new(Guid.NewGuid(), userName, password, returnUrl, rememberLogin);

    private static async Task AssertSecretAbsent(
        BddTestContext ctx,
        Guid accountId,
        string password,
        string returnUrl)
    {
        var events = await ctx.EventStore.ReadStreamAsync($"ShopperAccount:{accountId}");
        Assert.Contains(events, sequenced => sequenced.Event is ShopperSignedIn);
        foreach (var sequenced in events)
        {
            var json = JsonSerializer.Serialize(new StoredEvent(sequenced.Event, sequenced.Metadata));
            Assert.DoesNotContain(password, json, StringComparison.Ordinal);
            Assert.DoesNotContain(returnUrl, json, StringComparison.Ordinal);
        }
    }

    private sealed record StoredEvent(IEvent Event, EventMetadata Metadata);

    private sealed class FixedIssuer : IShopperAccessTokenIssuer
    {
        public const string Token = "test-access-token";

        public static readonly DateTimeOffset ExpiresAt = DateTimeOffset.UnixEpoch.AddYears(30);

        public Task<ShopperAccessToken> IssueAsync(
            Guid accountId,
            string displayName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ShopperAccessToken(Token, ExpiresAt));
    }
}
