namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// How long the orders page waits for an order that checkout just placed.
/// </summary>
/// <remarks>
/// The first read counts as one attempt. Later attempts wait <see cref="Delay"/>.
/// Ten attempts at 250 milliseconds is about two seconds after the first read.
/// </remarks>
public sealed class OrderListRefresh
{
    /// <summary>
    /// How many times to read the orders list, including the first read.
    /// </summary>
    public int Attempts { get; init; } = 10;

    /// <summary>
    /// Pause between reads when the placed order is still missing.
    /// </summary>
    public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(250);
}
