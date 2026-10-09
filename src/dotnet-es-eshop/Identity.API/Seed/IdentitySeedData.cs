using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Identity.Commands;
using LawnDart.Samples.EShop.Shared;

namespace LawnDart.Samples.EShop.Identity.API.Seed;

/// <summary>
/// One shopper copied from the eShop identity seed.
/// </summary>
public sealed record IdentitySeedUser(
    string UserName,
    string Email,
    string Password,
    string Name,
    string LastName,
    string PhoneNumber,
    string Street,
    string City,
    string State,
    string Country,
    string ZipCode,
    string CardHolderName,
    string CardNumber,
    int CardType,
    string Expiration,
    string SecurityNumber)
{
    /// <summary>
    /// Deterministic account id for this user name.
    /// </summary>
    public Guid AccountId => EShopSeedIds.Create("shopper-account:" + UserName);

    /// <summary>
    /// Register Shopper Account for this user.
    /// </summary>
    /// <returns>The command. It does not mark the email confirmed.</returns>
    public RegisterShopperAccountCommand RegisterCommand() =>
        new(
            EShopSeedIds.Create("register-shopper-account:" + UserName),
            AccountId,
            UserName,
            Email,
            Password,
            Name,
            LastName);

    /// <summary>
    /// Update Shopper Profile for the address and phone.
    /// </summary>
    /// <returns>The command.</returns>
    public UpdateShopperProfileCommand ProfileCommand() =>
        new(
            EShopSeedIds.Create("update-shopper-profile:" + UserName),
            AccountId,
            Name,
            LastName,
            PhoneNumber,
            Street,
            City,
            State,
            Country,
            ZipCode);

    /// <summary>
    /// Demo payment card for this user. The seeder writes it on the login record only.
    /// </summary>
    /// <returns>The card copied from eShop.</returns>
    public ShopperPaymentCard PaymentCard() =>
        new(CardHolderName, CardNumber, CardType, Expiration, SecurityNumber);
}

/// <summary>
/// Alice and Bob from the eShop identity seed.
/// </summary>
public static class IdentitySeedData
{
    /// <summary>
    /// Seeded shoppers, Alice then Bob.
    /// </summary>
    public static readonly IReadOnlyList<IdentitySeedUser> Users =
    [
        new(
            "alice",
            "AliceSmith@email.com",
            "Pass123$",
            "Alice",
            "Smith",
            "1234567890",
            "15703 NE 61st Ct",
            "Redmond",
            "WA",
            "U.S.",
            "98052",
            "Alice Smith",
            "XXXXXXXXXXXX1881",
            1,
            "12/24",
            "123"),
        new(
            "bob",
            "BobSmith@email.com",
            "Pass123$",
            "Bob",
            "Smith",
            "1234567890",
            "15703 NE 61st Ct",
            "Redmond",
            "WA",
            "U.S.",
            "98052",
            "Bob Smith",
            "XXXXXXXXXXXX1881",
            1,
            "12/24",
            "456")
    ];
}
