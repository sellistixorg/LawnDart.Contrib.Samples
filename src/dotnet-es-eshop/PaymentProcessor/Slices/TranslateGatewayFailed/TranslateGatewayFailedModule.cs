using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.TranslateGatewayFailed;

/// <summary>
/// Registers the gateway failure subscription on the PaymentProcessor host.
/// </summary>
internal sealed class TranslateGatewayFailedModule : IEShopSliceModule
{
    /// <summary>
    /// Registers <see cref="TranslateGatewayFailedReactor"/> and <see cref="TranslateGatewayFailedSubscription"/>.
    /// </summary>
    /// <param name="services">PaymentProcessor service collection.</param>
    /// <param name="configuration">Host configuration. This slice does not read it.</param>
    /// <remarks>
    /// The callback is not a stored event. <c>AddEShopReactors</c> does not subscribe to it.
    /// The reactor is transient. <see cref="TranslateGatewayFailedSubscription"/> resolves it inside a scope for each message.
    /// A second call leaves one hosted service.
    /// </remarks>
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddTransient<TranslateGatewayFailedReactor>();
        services.AddHostedService<TranslateGatewayFailedSubscription>();
    }
}
