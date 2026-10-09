namespace LawnDart.Samples.EShop.PaymentProcessor.Gateway;

/// <summary>
/// Sends a charge request to the payment gateway and returns the outcome.
/// </summary>
/// <remarks>
/// Charge Order Payment calls this port. The simulated gateway is registered with that slice.
/// Translate Gateway Succeeded and Translate Gateway Failed read <see cref="PaymentGatewaySucceeded"/> and <see cref="PaymentGatewayFailed"/>.
/// </remarks>
public interface IPaymentGateway
{
    /// <summary>
    /// Charges the attempt and returns success or failure.
    /// </summary>
    /// <param name="request">The charge to send.</param>
    /// <param name="cancellationToken">Token used to cancel the charge.</param>
    /// <returns>A <see cref="PaymentGatewaySucceeded"/> or <see cref="PaymentGatewayFailed"/> result.</returns>
    Task<PaymentGatewayResult> ChargeAsync(
        PaymentChargeRequest request,
        CancellationToken cancellationToken = default);
}
