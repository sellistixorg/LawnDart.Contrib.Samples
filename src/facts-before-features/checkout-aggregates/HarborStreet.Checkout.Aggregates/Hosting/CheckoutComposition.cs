using HarborStreet.Checkout.Aggregates.Checkout;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LawnDart;
using LawnDart.Aggregates;
using LawnDart.EventSourcing;
using LawnDart.EventStore;

namespace HarborStreet.Checkout.Aggregates.Hosting;

public static class CheckoutComposition
{
    public const string ContextName = "default";

    public static IServiceCollection AddAggregateCheckout(
        this IServiceCollection services,
        string dataDirectory,
        Action<ILoggingBuilder>? configureLogging = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        services.AddLogging(b =>
        {
            b.AddConsole().SetMinimumLevel(LogLevel.Warning);
            configureLogging?.Invoke(b);
        });

        services.AddLawnDart(o =>
        {
            o.RequireTenantId = false;
            o.EnableAuthorization = false;
        });

        services
            .AddBoundedContext(ContextName)
            .UseInMemory()
            .WithEventTypes(CheckoutEventTypes.All);

        services.AddSingleton(sp =>
        {
            var repo = sp.GetRequiredKeyedService<IAggregateRepository>(ContextName);
            return new AggregateCheckoutChoreography(repo);
        });

        return services;
    }
}
