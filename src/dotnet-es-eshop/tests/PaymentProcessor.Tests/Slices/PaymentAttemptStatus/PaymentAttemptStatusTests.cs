using System.Net;
using System.Text.Json;
using LawnDart.EventSourcing;
using LawnDart.EventStore;
using LawnDart.Projections.Lightweight;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.PaymentProcessor.Aggregates.PaymentAttempt;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;
using LawnDart.Samples.EShop.PaymentProcessor.Slices.PaymentAttemptStatus;
using LawnDart.Samples.EShop.Shared.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.PaymentProcessor.Tests.Slices.PaymentAttemptStatus;

public class PaymentAttemptStatusTests
{
    [Fact]
    public void Charge_then_success_or_failure_folds_the_attempt()
    {
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var projection = new PaymentAttemptStatusProjection();

        projection.ProcessEvent(Requested(attemptId, orderId, 42.50m), 1);
        var requested = projection.GetView();
        Assert.Equal(attemptId, requested.AttemptId);
        Assert.Equal(orderId, requested.OrderId);
        Assert.Equal(42.50m, requested.Amount);
        Assert.Equal(PaymentAttempt.RequestedStatus, requested.Status);
        Assert.Null(requested.GatewayReference);
        Assert.Null(requested.FailureReason);

        projection.ProcessEvent(
            new PaymentSucceeded(Guid.NewGuid(), DateTime.UtcNow, attemptId, orderId, "gw-1", null),
            2);
        var succeeded = projection.GetView();
        Assert.Equal(PaymentAttempt.SucceededStatus, succeeded.Status);
        Assert.Equal("gw-1", succeeded.GatewayReference);
        Assert.Equal(42.50m, succeeded.Amount);
        Assert.Equal(orderId, succeeded.OrderId);
        Assert.Null(succeeded.FailureReason);

        var failedAttempt = Guid.NewGuid();
        var failedOrder = Guid.NewGuid();
        var failed = new PaymentAttemptStatusProjection();
        failed.ProcessEvent(Requested(failedAttempt, failedOrder, 10m), 1);
        failed.ProcessEvent(
            new PaymentFailed(Guid.NewGuid(), DateTime.UtcNow, failedAttempt, failedOrder, "Card declined"),
            2);
        var view = failed.GetView();
        Assert.Equal(PaymentAttempt.FailedStatus, view.Status);
        Assert.Equal("Card declined", view.FailureReason);
        Assert.Equal(10m, view.Amount);
        Assert.Null(view.GatewayReference);
        Assert.Equal(failedOrder, view.OrderId);
    }

    [Fact]
    public void Payment_failed_keeps_the_requested_order_id()
    {
        var attemptId = Guid.NewGuid();
        var requestedOrder = Guid.NewGuid();
        var otherOrder = Guid.NewGuid();
        var projection = new PaymentAttemptStatusProjection();

        projection.ProcessEvent(Requested(attemptId, requestedOrder, 10m), 1);
        projection.ProcessEvent(
            new PaymentFailed(Guid.NewGuid(), DateTime.UtcNow, attemptId, otherOrder, "Card declined"),
            2);

        var view = projection.GetView();
        Assert.Equal(PaymentAttempt.FailedStatus, view.Status);
        Assert.Equal("Card declined", view.FailureReason);
        Assert.Equal(requestedOrder, view.OrderId);
        Assert.Equal(10m, view.Amount);
        Assert.Null(view.GatewayReference);
    }

    [Fact]
    public void Charged_amount_replaces_the_requested_amount()
    {
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var projection = new PaymentAttemptStatusProjection();

        projection.ProcessEvent(Requested(attemptId, orderId, 10m), 1);
        projection.ProcessEvent(
            new PaymentSucceeded(Guid.NewGuid(), DateTime.UtcNow, attemptId, orderId, null, 9.5m),
            2);

        var view = projection.GetView();
        Assert.Equal(9.5m, view.Amount);
        Assert.Equal(PaymentAttempt.SucceededStatus, view.Status);
        Assert.Null(view.GatewayReference);
    }

    [Fact]
    public async Task Get_returns_requested_then_succeeded()
    {
        await using var host = await StatusHost.StartAsync();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);

        await store.AppendAsync(
            PaymentAttemptStatusProjection.InstanceId(attemptId),
            [Requested(attemptId, orderId, 18m)]);

        var requested = await host.PollAsync(attemptId, view => view.Status == PaymentAttempt.RequestedStatus);
        Assert.Equal(attemptId, requested.AttemptId);
        Assert.Equal(orderId, requested.OrderId);
        Assert.Equal(18m, requested.Amount);
        Assert.Null(requested.GatewayReference);
        Assert.Null(requested.FailureReason);

        await store.AppendAsync(
            PaymentAttemptStatusProjection.InstanceId(attemptId),
            [new PaymentSucceeded(Guid.NewGuid(), DateTime.UtcNow, attemptId, orderId, "ref-9", 18m)]);

        var succeeded = await host.PollAsync(attemptId, view => view.Status == PaymentAttempt.SucceededStatus);
        Assert.Equal("ref-9", succeeded.GatewayReference);
        Assert.Equal(18m, succeeded.Amount);
        Assert.Null(succeeded.FailureReason);

        using var response = await host.Client.GetAsync(StatusHost.Url(attemptId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertModelFields(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_returns_failed()
    {
        await using var host = await StatusHost.StartAsync();
        var attemptId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var store = host.Services.GetRequiredKeyedService<IEventStore>(EShopLawnDartHost.ContextName);

        await store.AppendAsync(
            PaymentAttemptStatusProjection.InstanceId(attemptId),
            [
                Requested(attemptId, orderId, 7m),
                new PaymentFailed(Guid.NewGuid(), DateTime.UtcNow, attemptId, orderId, "Insufficient funds")
            ]);

        var view = await host.PollAsync(attemptId, item => item.Status == PaymentAttempt.FailedStatus);
        Assert.Equal(7m, view.Amount);
        Assert.Equal("Insufficient funds", view.FailureReason);
        Assert.Null(view.GatewayReference);
        Assert.Equal(orderId, view.OrderId);
    }

    [Fact]
    public async Task Unknown_id_returns_404()
    {
        await using var host = await StatusHost.StartAsync();

        var missing = await host.Client.GetAsync(StatusHost.Url(Guid.NewGuid()));
        var empty = await host.Client.GetAsync(StatusHost.Url(Guid.Empty));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, empty.StatusCode);
    }

    private static void AssertModelFields(string json)
    {
        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name);
        Assert.Equal(
            new[]
            {
                "amount",
                "attemptId",
                "failureReason",
                "gatewayReference",
                "orderId",
                "status"
            },
            names);
    }

    private static PaymentChargeRequested Requested(Guid attemptId, Guid orderId, decimal amount) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            attemptId,
            orderId,
            amount,
            PaymentAttempt.RequestedStatus,
            "USD");

    private sealed class StatusHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private StatusHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public IServiceProvider Services => _app.Services;

        public static async Task<StatusHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddLawnDart(options => options.RequireTenantId = false);
            builder.Services.AddInMemoryProjectionStores(EShopLawnDartHost.ContextName);
            var context = builder.Services.AddBoundedContext(EShopLawnDartHost.ContextName);
            context.UseInMemory();
            context.AddPaymentContext(
                typeof(PaymentAttemptStatusProjection).Assembly,
                typeof(PaymentSucceeded).Assembly);

            var app = builder.Build();
            app.MapEShopSlices(typeof(PaymentAttemptStatusProjection).Assembly);
            await app.StartAsync();

            var address = app.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .Single();
            return new StatusHost(app, new HttpClient { BaseAddress = new Uri(address) });
        }

        public async Task<PaymentAttemptStatusView> PollAsync(
            Guid attemptId,
            Func<PaymentAttemptStatusView, bool> matches)
        {
            string? lastBody = null;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                using var response = await Client.GetAsync(Url(attemptId));
                lastBody = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var view = JsonSerializer.Deserialize<PaymentAttemptStatusView>(
                        lastBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (view is not null && matches(view))
                        return view;
                }

                await Task.Delay(100);
            }

            Assert.Fail($"Payment attempt status was not ready. Last body: {lastBody}");
            throw new InvalidOperationException(lastBody);
        }

        public static string Url(Guid attemptId) =>
            $"/views/payment-attempt-status/{attemptId:D}";

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
