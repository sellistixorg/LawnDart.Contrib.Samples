using HarborStreet.Coordination.Coordination;
using HarborStreet.Coordination.Payments;
using HarborStreet.Coordination.Reactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LawnDart;
using LawnDart.Aggregates;
using LawnDart.EventSourcing;
using LawnDart.EventStore;

namespace HarborStreet.Coordination.Hosting;

public static class CoordinationComposition
{
    public const string ContextName = "default";

    public static IServiceCollection AddHarborStreetCoordination(
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
            .WithEventTypes(CoordinationEventTypes.All);

        var fake = gateway ?? new FakePaymentGateway();
        services.AddSingleton(fake);
        services.AddSingleton<IPaymentGateway>(fake);
        services.AddSingleton(sp =>
        {
            var repo = sp.GetRequiredKeyedService<IAggregateRepository>(ContextName);
            return new PaymentFollowOnReactions(repo);
        });
        services.AddSingleton(sp =>
        {
            var repo = sp.GetRequiredKeyedService<IAggregateRepository>(ContextName);
            return new PaymentCaptureService(
                repo,
                sp.GetRequiredService<IPaymentGateway>(),
                sp.GetRequiredService<PaymentFollowOnReactions>());
        });

        return services;
    }
}
