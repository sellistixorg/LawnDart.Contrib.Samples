using LawnDart;
using LawnDart.EventStore;
using LawnDart.Samples.EShop.Shared;

namespace LawnDart.Samples.EShop.Shared.Events;

/// <summary>
/// Grace period elapsed for a submitted order. Ordering issues Mark Order Awaiting Validation.
/// </summary>
/// <remarks>
/// The token is <c>grace-period-confirmed</c>. It carries the order id.
/// OrderProcessor publishes it when the grace period has elapsed. The message id is stable for that order.
/// </remarks>
/// <param name="Id">Event identifier. Stable for one order id so a republish is the same message.</param>
/// <param name="Timestamp">When the grace period was confirmed.</param>
/// <param name="OrderId">Order id passed to Mark Order Awaiting Validation.</param>
[EventTypeName("grace-period-confirmed")]
public sealed record GracePeriodConfirmed(
    Guid Id,
    DateTime Timestamp,
    Guid OrderId) : IEvent
{
    /// <summary>
    /// RFC 4122 DNS namespace. The name for <see cref="IdFor"/> is
    /// <c>grace-period-confirmed:{orderId}</c>.
    /// </summary>
    public static readonly Guid DnsNamespace = EShopVersion5Id.DnsNamespace;

    /// <summary>
    /// Message id for <paramref name="orderId"/>.
    /// A restart that confirms the same order publishes this same id.
    /// </summary>
    /// <param name="orderId">Order id.</param>
    /// <returns>The version 5 id used as <see cref="Id"/> and as the transport message id.</returns>
    public static Guid IdFor(Guid orderId) =>
        EShopVersion5Id.ForPrefixedName("grace-period-confirmed", orderId.ToString("D"));
}
