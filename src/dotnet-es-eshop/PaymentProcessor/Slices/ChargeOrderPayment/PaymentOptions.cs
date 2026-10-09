namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;

/// <summary>
/// eShop payment simulation flag. The simulated gateway reads PaymentSucceeded and does not call a real gateway.
/// </summary>
public sealed class PaymentOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "PaymentOptions";

    /// <summary>
    /// When true, the simulated gateway returns success. When false, it returns failure. Defaults to true.
    /// </summary>
    public bool PaymentSucceeded { get; set; } = true;
}
