using HarborStreet.Checkout.Dcb.Checkout;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LawnDart;
using LawnDart.Dcb;
using LawnDart.EventSourcing;
using LawnDart.EventStore;

namespace HarborStreet.Checkout.Dcb.Hosting;

public static class DcbCheckoutComposition
{
    public const string ContextName = "default";

    public static IServiceCollection AddDcbCheckout(
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

        return services;
    }

    public static DcbCheckoutService CreateCheckoutService(IServiceProvider sp)
    {
        var dcb = sp.GetRequiredKeyedService<IDcbRepository>(ContextName);
        return new DcbCheckoutService(dcb);
    }
}
