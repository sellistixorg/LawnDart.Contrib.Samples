using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LawnDart.Samples.EShop.Infrastructure;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Calls Identity Sign In Shopper.
/// </summary>
public sealed class ShopperSignInClient(IHttpClientFactory httpClientFactory)
{
    /// <summary>
    /// Message shown when the user name or password is rejected.
    /// </summary>
    public const string InvalidCredentialsMessage =
        "User name or password is incorrect, or account is locked/disabled.";

    /// <summary>
    /// Message shown when Identity cannot be reached.
    /// </summary>
    public const string UnavailableMessage = "Sign-in is temporarily unavailable.";

    /// <summary>
    /// Posts the user name and the password.
    /// </summary>
    /// <param name="userName">User name. An email address is not accepted.</param>
    /// <param name="password">Password. This method does not log it.</param>
    /// <param name="rememberLogin">Persist the storefront cookie.</param>
    /// <param name="returnUrl">Local return path sent with the command. Not applied by Identity.</param>
    /// <param name="cancellationToken">Token used to cancel the post.</param>
    /// <returns>The account id, display name, and access token, a rejection, or an unavailable result.</returns>
    /// <remarks>
    /// POST /api/identity/sign-in-shopper on the identity HTTP client.
    /// A 202 body supplies the account id, display name, access token, and token expiry.
    /// A 422 is a rejection. The password and the access token are not copied into an exception message.
    /// This method does not log the password or the token.
    /// </remarks>
    public async Task<ShopperSignInOutcome> SignInAsync(
        string userName,
        string password,
        bool rememberLogin,
        string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(EShopStorefrontClients.Identity);
        if (client.BaseAddress is null)
            return ShopperSignInOutcome.ServiceUnavailable();

        try
        {
            using var response = await client.PostAsJsonAsync(
                "/api/identity/sign-in-shopper",
                new
                {
                    Id = Guid.NewGuid(),
                    UserName = userName,
                    Password = password,
                    ReturnUrl = returnUrl,
                    RememberLogin = rememberLogin
                },
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
                return ShopperSignInOutcome.CredentialsRejected();

            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                response.EnsureSuccessStatusCode();
                return ShopperSignInOutcome.ServiceUnavailable();
            }

            var body = await response.Content.ReadFromJsonAsync<ShopperSignInBody>(cancellationToken)
                .ConfigureAwait(false);
            if (body is null
                || body.AccountId == Guid.Empty
                || string.IsNullOrEmpty(body.AccessToken)
                || body.AccessTokenExpiresAt == default)
            {
                return ShopperSignInOutcome.ServiceUnavailable();
            }

            return ShopperSignInOutcome.Success(
                body.AccountId,
                body.DisplayName ?? "",
                body.AccessToken,
                body.AccessTokenExpiresAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return ShopperSignInOutcome.ServiceUnavailable();
        }
        catch (JsonException)
        {
            return ShopperSignInOutcome.ServiceUnavailable();
        }
        catch (NotSupportedException)
        {
            return ShopperSignInOutcome.ServiceUnavailable();
        }
        catch (TaskCanceledException)
        {
            return ShopperSignInOutcome.ServiceUnavailable();
        }
    }

    private sealed class ShopperSignInBody
    {
        public Guid AccountId { get; set; }

        public string? DisplayName { get; set; }

        public string? AccessToken { get; set; }

        public DateTimeOffset AccessTokenExpiresAt { get; set; }
    }
}

/// <summary>
/// Result of a storefront sign-in call.
/// </summary>
public sealed class ShopperSignInOutcome
{
    private ShopperSignInOutcome(
        bool rejected,
        bool unavailable,
        Guid accountId,
        string displayName,
        string accessToken,
        DateTimeOffset accessTokenExpiresAt)
    {
        Rejected = rejected;
        Unavailable = unavailable;
        AccountId = accountId;
        DisplayName = displayName;
        AccessToken = accessToken;
        AccessTokenExpiresAt = accessTokenExpiresAt;
    }

    /// <summary>
    /// True when Identity rejected the user name or password.
    /// </summary>
    public bool Rejected { get; }

    /// <summary>
    /// True when Identity did not return an account id.
    /// </summary>
    public bool Unavailable { get; }

    /// <summary>
    /// Account id. Empty unless <see cref="Succeeded"/> is true.
    /// </summary>
    public Guid AccountId { get; }

    /// <summary>
    /// Display name. Empty unless <see cref="Succeeded"/> is true.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Access token. Empty unless <see cref="Succeeded"/> is true. Do not log this value.
    /// </summary>
    public string AccessToken { get; }

    /// <summary>
    /// UTC expiry of <see cref="AccessToken"/>.
    /// </summary>
    public DateTimeOffset AccessTokenExpiresAt { get; }

    /// <summary>
    /// True when the call returned an account id and an access token.
    /// </summary>
    public bool Succeeded =>
        !Rejected && !Unavailable && AccountId != Guid.Empty && AccessToken.Length > 0;

    /// <summary>
    /// A successful sign-in.
    /// </summary>
    /// <param name="accountId">Shopper account id.</param>
    /// <param name="displayName">Name for the cookie.</param>
    /// <param name="accessToken">Access token stored in the cookie. Do not log it.</param>
    /// <param name="accessTokenExpiresAt">UTC expiry of <paramref name="accessToken"/>.</param>
    /// <returns>The success result.</returns>
    public static ShopperSignInOutcome Success(
        Guid accountId,
        string displayName,
        string accessToken,
        DateTimeOffset accessTokenExpiresAt) =>
        new(false, false, accountId, displayName, accessToken, accessTokenExpiresAt);

    /// <summary>
    /// Identity rejected the credentials.
    /// </summary>
    /// <returns>The rejection result.</returns>
    public static ShopperSignInOutcome CredentialsRejected() =>
        new(true, false, Guid.Empty, "", "", default);

    /// <summary>
    /// Identity could not be used.
    /// </summary>
    /// <returns>The unavailable result.</returns>
    public static ShopperSignInOutcome ServiceUnavailable() =>
        new(false, true, Guid.Empty, "", "", default);
}
