using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.PaymentAttemptStatus;

/// <summary>
/// Maps the payment attempt status view route.
/// </summary>
internal sealed class PaymentAttemptStatusModule : IEShopSliceModule
{
    /// <summary>
    /// Maps GET /views/payment-attempt-status/{attemptId}.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapPaymentAttemptStatus();
}
