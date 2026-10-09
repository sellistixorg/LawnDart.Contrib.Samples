using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.PaymentProcessor.Gateway;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LawnDart.Samples.EShop.PaymentProcessor.Slices.ChargeOrderPayment;

/// <summary>
/// Registers the simulated payment gateway on the PaymentProcessor host.
/// </summary>
internal sealed class ChargeOrderPaymentModule : IEShopSliceModule
{
    /// <summary>
    /// Binds <see cref="PaymentOptions"/> and registers <see cref="SimulatedPaymentGateway"/>.
    /// </summary>
    /// <param name="services">PaymentProcessor service collection.</param>
    /// <param name="configuration">Host configuration. The <see cref="PaymentOptions.SectionName"/> section is bound.</param>
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<PaymentOptions>()
            .Bind(configuration.GetSection(PaymentOptions.SectionName));
        services.TryAddSingleton<IPaymentGateway, SimulatedPaymentGateway>();
    }
}
