namespace LawnDart.Samples.EShop.Identity.API.Login;

/// <summary>
/// RSA private key for shopper access tokens. One row is enough.
/// </summary>
/// <remarks>
/// The bytes are PKCS#8. They are created on first startup and stored in <c>identitylogindb</c>.
/// They are not checked into source. Do not log them.
/// </remarks>
public sealed class ShopperSigningKey
{
    /// <summary>
    /// Key id. It is published as <c>kid</c> on the token and in the JWKS document.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// PKCS#8 private key. Do not log this value.
    /// </summary>
    public byte[] Pkcs8PrivateKey { get; set; } = [];

    /// <summary>
    /// UTC time the row was inserted.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }
}
