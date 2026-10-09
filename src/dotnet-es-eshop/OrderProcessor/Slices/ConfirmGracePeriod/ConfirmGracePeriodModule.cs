using LawnDart.Projections.Storage;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LawnDart.Samples.EShop.OrderProcessor.Slices.ConfirmGracePeriod;

/// <summary>
/// Registers the grace-period timer, options, and pending-order list on the OrderProcessor host.
/// </summary>
internal sealed class ConfirmGracePeriodModule : IEShopSliceModule
{
    /// <summary>
    /// Registers <see cref="BackgroundTaskOptions"/>, the pending-order list, and <see cref="GracePeriodManagerService"/>.
    /// </summary>
    /// <param name="services">OrderProcessor service collection.</param>
    /// <param name="configuration">Host configuration. The <see cref="BackgroundTaskOptions.SectionName"/> section is bound.</param>
    /// <remarks>
    /// The Order Started reactor is registered by the assembly scan. The timer publishes grace-period-confirmed.
    /// </remarks>
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<BackgroundTaskOptions>()
            .Bind(configuration.GetSection(BackgroundTaskOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new PendingGraceOrders(
            sp.GetRequiredKeyedService<IViewStore>(EShopLawnDartHost.ContextName)));
        services.AddHostedService<GracePeriodManagerService>();
    }
}
