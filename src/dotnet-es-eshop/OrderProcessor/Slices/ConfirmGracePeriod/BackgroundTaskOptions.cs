namespace LawnDart.Samples.EShop.OrderProcessor.Slices.ConfirmGracePeriod;

/// <summary>
/// OrderProcessor CheckUpdateTime / GracePeriodTime.
/// </summary>
public sealed class BackgroundTaskOptions
{
    /// <summary>
    /// Configuration section name. Matches eShop <c>BackgroundTaskOptions</c>.
    /// </summary>
    public const string SectionName = "BackgroundTaskOptions";

    /// <summary>
    /// Default GracePeriodTime in minutes. eShop's default is 1.
    /// </summary>
    public const int DefaultGracePeriodMinutes = 1;

    /// <summary>
    /// Default CheckUpdateTime in seconds. eShop's default is 30.
    /// </summary>
    public const int DefaultCheckUpdateSeconds = 30;

    /// <summary>
    /// GracePeriodTime minutes. Orders submitted at least this long are confirmed.
    /// </summary>
    public int GracePeriodTime { get; set; } = DefaultGracePeriodMinutes;

    /// <summary>
    /// Seconds between grace-period checks.
    /// </summary>
    public int CheckUpdateTime { get; set; } = DefaultCheckUpdateSeconds;
}
