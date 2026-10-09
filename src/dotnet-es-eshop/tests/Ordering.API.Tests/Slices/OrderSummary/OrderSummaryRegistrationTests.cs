using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Projections.Lightweight.Registration;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.Ordering.API.Slices.OrderSummary;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.OrderSummary;

public class OrderSummaryRegistrationTests
{
    [Fact]
    public void Ordering_assembly_scan_registers_the_order_summary_projection()
    {
        var services = new ServiceCollection();
        services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
        var context = services.AddBoundedContext(EShopLawnDartHost.ContextName);
        context.AddOrderingContext(
            typeof(OrderSummaryProjection).Assembly,
            typeof(OrderPaid).Assembly);

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredKeyedService<IReadOnlyList<ProjectionRegistration>>(
            EShopLawnDartHost.ContextName);
        var registration = Assert.Single(
            registrations,
            item => item.HandlerType == typeof(OrderSummaryProjection));

        Assert.Equal(OrderSummaryProjection.StorageKey, registration.StorageKey);
        Assert.Equal(ProjectionKind.SingleStream, registration.Kind);
        Assert.Equal(typeof(OrderSummaryView), registration.ViewType);
    }

    [Fact]
    public void Slice_discovery_maps_the_order_summary_route()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.MapEShopSlices(typeof(OrderSummaryProjection).Assembly);

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.Contains(OrderSummaryEndpoint.Route, routes);
    }
}
