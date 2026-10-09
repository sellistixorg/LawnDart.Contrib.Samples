using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Identity.Commands;
using LawnDart.Samples.EShop.Infrastructure;

namespace LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;

/// <summary>
/// Aggregate root for a shopper identity account (maps to ASP.NET Identity ApplicationUser in eShop Identity.API). Stream key: ShopperAccount:{accountId}.
/// </summary>
public sealed partial class ShopperAccount : AggregateRoot<ShopperAccountState>
{
    /// <summary>
    /// Shopper account does not exist. Update Shopper Profile rejects a missing account.
    /// Sign Out Shopper rejects a missing or inactive account with the same message.
    /// </summary>
    public const string NotFoundMessage = "Shopper account does not exist.";

    /// <summary>
    /// Error code <c>SHOPPER_ACCOUNT_NOT_FOUND</c>.
    /// </summary>
    public const string NotFoundTitle = "SHOPPER_ACCOUNT_NOT_FOUND";

    /// <inheritdoc />
    protected override void ApplyEventToState(IEvent @event) => EShopStateApplier.Apply(State, @event);

    internal static PreparedRegistration Prepare(RegisterShopperAccountCommand command)
    {
        if (command.AccountId == Guid.Empty)
            throw new DomainException("Account id is required.");

        var userName = Required(command.UserName, "User name is required.");
        var email = Required(command.Email, "Email is required.");
        _ = Required(command.Password, "Password is required.");
        var name = Required(command.Name, "Name is required.");
        var lastName = Required(command.LastName, "Last name is required.");
        return new PreparedRegistration(userName, email, name, lastName);
    }

    private static string Required(string? value, string message)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new DomainException(message);

        return trimmed;
    }

    internal readonly record struct PreparedRegistration(
        string UserName,
        string Email,
        string Name,
        string LastName);
}
