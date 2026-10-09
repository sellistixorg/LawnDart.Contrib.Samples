using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Registration;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.PaymentAttemptStatus;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests.Slices.PaymentAttemptStatus;

public class PaymentAttemptStatusRegistrationTests
{
    [Fact]
    public void Payment_assembly_scan_registers_the_attempt_status_projection()
    {
        var services = new ServiceCollection();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.AddPaymentContext(
            typeof(PaymentAttemptStatusProjection).Assembly,
            typeof(PaymentSucceeded).Assembly);

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredKeyedService<IReadOnlyList<ProjectionRegistration>>(
            EShopLawnDartHost.ContextName);
        var registration = Assert.Single(
            registrations,
            item => item.HandlerType == typeof(PaymentAttemptStatusProjection));

        Assert.Equal(PaymentAttemptStatusProjection.StorageKey, registration.StorageKey);
        Assert.Equal(ProjectionKind.SingleStream, registration.Kind);
        Assert.Equal(typeof(PaymentAttemptStatusView), registration.ViewType);
    }

    [Fact]
    public void Slice_discovery_maps_the_attempt_status_route()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.MapEShopSlices(typeof(PaymentAttemptStatusProjection).Assembly);

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.Contains(PaymentAttemptStatusEndpoint.Route, routes);
    }
}
