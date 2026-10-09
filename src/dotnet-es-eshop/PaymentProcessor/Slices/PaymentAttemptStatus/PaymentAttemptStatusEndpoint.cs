using System.Text.Json;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Hosting;
using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.PaymentAttemptStatus;

/// <summary>
/// Projection of payment attempt status for an order.
/// </summary>
public static class PaymentAttemptStatusEndpoint
{
    /// <summary>
    /// GET /views/payment-attempt-status/{attemptId}.
    /// </summary>
    public const string Route = "/views/payment-attempt-status/{attemptId:guid}";

    /// <summary>
    /// Projection of payment attempt status for an order.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same route builder.</returns>
    /// <remarks>
    /// An unknown attempt id returns not found. The response fields are attempt id, order id, amount, status, gateway reference, and failure detail.
    /// </remarks>
    public static IEndpointRouteBuilder MapPaymentAttemptStatus(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(Route, GetAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid attemptId,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (attemptId == Guid.Empty)
            return Results.NotFound();

        var json = await ReadAttemptJsonAsync(httpContext.RequestServices, attemptId, cancellationToken)
            .ConfigureAwait(false);
        if (json is null)
            return Results.NotFound();

        var view = JsonSerializer.Deserialize<PaymentAttemptStatusView>(json, ProjectionViewJson.Read);
        if (view is null || view.AttemptId == Guid.Empty)
            return Results.NotFound();

        return Results.Ok(view);
    }

    private static async Task<string?> ReadAttemptJsonAsync(
        IServiceProvider services,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var instanceId = PaymentAttemptStatusProjection.InstanceId(attemptId);
        var contextName = EShopLawnDartHost.ContextName;
        var runners = services.GetKeyedService<IProjectionRunnerManager>(contextName);
        if (runners is not null
            && runners.TryGetView(PaymentAttemptStatusProjection.StorageKey, instanceId, out var liveJson, out _))
        {
            return liveJson;
        }

        var views = services.GetRequiredKeyedService<IViewStore>(contextName);
        return await views.GetViewAsync(
            PaymentAttemptStatusProjection.StorageKey,
            instanceId,
            cancellationToken).ConfigureAwait(false);
    }
}
