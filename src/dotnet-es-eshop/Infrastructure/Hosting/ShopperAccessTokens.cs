using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Compact shopper access token.
/// </summary>
/// <param name="Value">Compact JWT. This value is a secret. Do not log it.</param>
/// <param name="ExpiresAt">UTC expiry.</param>
public sealed record ShopperAccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// Signs and checks shopper access tokens.
/// </summary>
/// <remarks>
/// The issuer is <see cref="Issuer"/>. The audience is <see cref="Audience"/>.
/// The signature algorithm is RSA SHA-256. A token lasts <see cref="Lifetime"/>.
/// The <c>sub</c> claim is the shopper account id. The <c>name</c> claim is the display name.
/// </remarks>
public static class ShopperAccessTokens
{
    /// <summary>
    /// JWT issuer. This is a logical name, not the Identity URL.
    /// </summary>
    public const string Issuer = "eshop-identity";

    /// <summary>
    /// JWT audience for Basket, Ordering, and Identity shopper routes.
    /// </summary>
    public const string Audience = "eshop";

    /// <summary>
    /// Authentication scheme that validates these tokens.
    /// </summary>
    public const string AuthenticationScheme = "ShopperBearer";

    /// <summary>
    /// Public key document. Identity maps this path.
    /// </summary>
    public const string JwksPath = "/.well-known/jwks.json";

    /// <summary>
    /// Discovery document. <c>jwks_uri</c> points at <see cref="JwksPath"/>.
    /// </summary>
    public const string OpenIdConfigurationPath = "/.well-known/openid-configuration";

    /// <summary>
    /// How long a failed key fetch waits before another try.
    /// </summary>
    public static readonly TimeSpan JwksFailureCooldown = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Minimum time between JWKS fetches. A fetch starts this wait whether it succeeded or failed.
    /// Tests may set a shorter value and should put the previous value back.
    /// </summary>
    public static TimeSpan JwksRefreshInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long one key fetch may wait. A down Identity host does not hold every request longer than this.
    /// </summary>
    public static readonly TimeSpan JwksFetchTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long a new token stays valid.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// Clock difference allowed when checking <c>exp</c>.
    /// </summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Creates a new RSA signing key. The private key stays in this process until the caller stores it.
    /// </summary>
    /// <returns>A 2048-bit RSA key with a new key id.</returns>
    public static RsaSecurityKey CreateKey()
    {
        var rsa = RSA.Create(2048);
        return new RsaSecurityKey(rsa)
        {
            KeyId = Guid.NewGuid().ToString("N")
        };
    }

    /// <summary>
    /// Loads a PKCS#8 private key.
    /// </summary>
    /// <param name="pkcs8">PKCS#8 bytes. Do not log them.</param>
    /// <param name="keyId">Key id published in the JWT header and the JWKS document.</param>
    /// <returns>The signing key.</returns>
    public static RsaSecurityKey ImportPkcs8(ReadOnlySpan<byte> pkcs8, string keyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(pkcs8, out _);
        return new RsaSecurityKey(rsa)
        {
            KeyId = keyId
        };
    }

    /// <summary>
    /// Exports the PKCS#8 private key. Do not log the result.
    /// </summary>
    /// <param name="key">Signing key created by <see cref="CreateKey"/> or <see cref="ImportPkcs8"/>.</param>
    /// <returns>The private key bytes.</returns>
    public static byte[] ExportPkcs8(RsaSecurityKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Rsa is null)
            throw new InvalidOperationException("Shopper signing key has no RSA instance.");

        return key.Rsa.ExportPkcs8PrivateKey();
    }

    /// <summary>
    /// Signs a shopper access token.
    /// </summary>
    /// <param name="key">RSA signing key.</param>
    /// <param name="accountId">Shopper account id. Written as <c>sub</c>.</param>
    /// <param name="displayName">Display name. Written as <c>name</c>. The password is not an input.</param>
    /// <param name="expiresAt">UTC expiry. The default is <see cref="Lifetime"/> after <paramref name="issuedAt"/>.</param>
    /// <param name="issuedAt">UTC issue time. The default is now.</param>
    /// <returns>The compact token and its expiry. Do not log the token.</returns>
    public static ShopperAccessToken Issue(
        RsaSecurityKey key,
        Guid accountId,
        string? displayName,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? issuedAt = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (accountId == Guid.Empty)
            throw new ArgumentException("Shopper account id is required.", nameof(accountId));

        var now = issuedAt ?? DateTimeOffset.UtcNow;
        var expires = expiresAt ?? now.Add(Lifetime);
        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = accountId.ToString("D"),
                [JwtRegisteredClaimNames.Name] = displayName ?? ""
            }
        };
        return new ShopperAccessToken(handler.CreateToken(descriptor), expires);
    }

    /// <summary>
    /// Writes the public JWKS document for one key. The private exponent is left out.
    /// </summary>
    /// <param name="key">RSA signing key.</param>
    /// <returns>JSON with the public RSA key.</returns>
    public static string WritePublicJwks(RsaSecurityKey key) =>
        WritePublicJwks([key]);

    /// <summary>
    /// Writes the public JWKS document for every stored key. Private exponents are left out.
    /// </summary>
    /// <param name="keys">RSA signing keys.</param>
    /// <returns>JSON with one public RSA key per entry.</returns>
    public static string WritePublicJwks(IEnumerable<RsaSecurityKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var set = new JsonWebKeySet();
        foreach (var key in keys)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key.Rsa is null)
                throw new InvalidOperationException("Shopper signing key has no RSA instance.");

            var parameters = key.Rsa.ExportParameters(includePrivateParameters: false);
            set.Keys.Add(new JsonWebKey
            {
                Kty = JsonWebAlgorithmsKeyTypes.RSA,
                N = Base64UrlEncoder.Encode(parameters.Modulus),
                E = Base64UrlEncoder.Encode(parameters.Exponent),
                Kid = key.KeyId,
                Use = "sig",
                Alg = SecurityAlgorithms.RsaSha256
            });
        }

        return JsonSerializer.Serialize(set);
    }

    /// <summary>
    /// Validation rules shared by Basket, Ordering, and Identity.
    /// </summary>
    /// <returns>Issuer, audience, lifetime, and signature checks. The signing key is supplied by the host.</returns>
    public static TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ClockSkew = ClockSkew,
        NameClaimType = JwtRegisteredClaimNames.Name,
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
    };

    /// <summary>
    /// Reads <c>sub</c> from a validated principal.
    /// </summary>
    /// <param name="principal">Principal produced by the shopper bearer scheme.</param>
    /// <returns>The shopper account id, or null when <c>sub</c> is missing or empty.</returns>
    public static Guid? ReadAccountId(ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(value, out var accountId) && accountId != Guid.Empty ? accountId : null;
    }
}
