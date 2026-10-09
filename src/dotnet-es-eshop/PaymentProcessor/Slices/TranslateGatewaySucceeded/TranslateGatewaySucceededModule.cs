using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.TranslateGatewaySucceeded;

/// <summary>
/// Registers the gateway success subscriber on the PaymentProcessor host.
/// </summary>
internal sealed class TranslateGatewaySucceededModule : IEShopSliceModule
{
    /// <summary>
    /// Registers <see cref="TranslateGatewaySucceededHost"/>.
    /// </summary>
    /// <param name="services">PaymentProcessor service collection.</param>
    /// <param name="configuration">Host configuration. This subscriber reads no settings.</param>
    /// <remarks>
    /// A second call leaves one hosted service. <c>AddEShopSlices</c> calls this method.
    /// </remarks>
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton<TranslateGatewaySucceededMessageHost>();
        services.AddHostedService<TranslateGatewaySucceededHost>();
    }
}
