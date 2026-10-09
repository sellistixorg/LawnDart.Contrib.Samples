using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Identity.API.Aggregates.ShopperAccount;

namespace LawnDart.Samples.EShop.Identity.API.Slices.RegisterShopperAccount;

/// <summary>
/// Reserves one email address so a second shopper account cannot register it.
/// </summary>
public sealed class ShopperEmail : AggregateRoot<ShopperEmailState>
{
    /// <summary>
    /// Trims the email and compares it case-insensitively.
    /// </summary>
    /// <param name="email">Email address.</param>
    /// <returns>The trimmed email in lower case.</returns>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// Stream key for the reserved email.
    /// </summary>
    /// <param name="email">Email address.</param>
    /// <returns><c>ShopperEmail:{normalizedEmail}</c>.</returns>
    public static string EmailStreamId(string email) => $"ShopperEmail:{Normalize(email)}";

    /// <summary>
    /// Reserves the email for one account.
    /// </summary>
    /// <param name="command">The reservation command.</param>
    /// <remarks>
    /// An account with this email (or user name) already exists.
    /// </remarks>
    public void Handle(ReserveShopperEmailCommand command)
    {
        if (command.AccountId == Guid.Empty)
            throw new DomainException("Account id is required.");

        var email = Normalize(command.Email ?? "");
        if (email.Length == 0)
            throw new DomainException("Email is required.");

        if (State.Exists)
        {
            throw new DomainException(
                ShopperAccount.EmailAlreadyRegisteredMessage,
                ShopperAccount.EmailAlreadyRegisteredTitle);
        }

        Apply(new ShopperEmailReserved(Guid.NewGuid(), DateTime.UtcNow, email, command.AccountId));
    }

    protected override void ApplyEventToState(IEvent @event)
    {
        switch (@event)
        {
            case ShopperEmailReserved reserved:
                State.Email = reserved.Email;
                State.AccountId = reserved.AccountId;
                State.Exists = true;
                break;
        }
    }
}
