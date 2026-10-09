namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// The basket command was rejected. The bag can be read again.
/// </summary>
public sealed class BasketCommandRejectedException : Exception
{
    /// <summary>
    /// The basket command was rejected.
    /// </summary>
    /// <param name="message">Shopper-facing rejection text. This is not the raw response body.</param>
    public BasketCommandRejectedException(string message)
        : base(message)
    {
    }
}
