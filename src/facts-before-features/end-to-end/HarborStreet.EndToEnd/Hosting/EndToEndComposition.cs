using HarborStreet.EndToEnd.Bookstore;
using HarborStreet.EndToEnd.Payments;
using HarborStreet.EndToEnd.Reactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LawnDart;
using LawnDart.Aggregates;
using LawnDart.Dcb;
using LawnDart.EventSourcing;
using LawnDart.EventStore;

namespace HarborStreet.EndToEnd.Hosting;

public static class EndToEndComposition
{
    public const string ContextName = "default";

    public static IServiceCollection AddHarborStreetEndToEnd(
        this IServiceCollection services,
        string dataDirectory,
        FakePaymentGateway? gateway = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddLawnDart(o =>
        {
            o.RequireTenantId = false;
            o.EnableAuthorization = false;
        });

        services
            .AddBoundedContext(ContextName)
            .UseInMemory()
            .WithEventTypes(EndToEndEventTypes.All);

        var fake = gateway ?? new FakePaymentGateway();
        services.AddSingleton(fake);
        services.AddSingleton<IPaymentGateway>(fake);
        services.AddSingleton(sp =>
        {
            var orders = sp.GetRequiredKeyedService<IAggregateRepository>(ContextName);
            var dcb = sp.GetRequiredKeyedService<IDcbRepository>(ContextName);
            return new PaymentFollowOnReactions(orders, dcb);
        });
        services.AddSingleton(sp =>
        {
            var orders = sp.GetRequiredKeyedService<IAggregateRepository>(ContextName);
            return new PaymentCaptureService(
                orders,
                sp.GetRequiredService<IPaymentGateway>(),
                sp.GetRequiredService<PaymentFollowOnReactions>());
        });
        services.AddSingleton(sp =>
        {
            var dcb = sp.GetRequiredKeyedService<IDcbRepository>(ContextName);
            var orders = sp.GetRequiredKeyedService<IAggregateRepository>(ContextName);
            return new HarborStreetBookstore(
                dcb,
                orders,
                sp.GetRequiredService<PaymentCaptureService>());
        });

        return services;
    }
}
