namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// The place-order command was rejected. The message is shopper-facing text.
/// </summary>
public sealed class OrderingCommandRejectedException : Exception
{
    /// <summary>
    /// The place-order command was rejected.
    /// </summary>
    /// <param name="message">Shopper-facing rejection text. This is not the raw response body.</param>
    public OrderingCommandRejectedException(string message)
        : base(message)
    {
    }
}
