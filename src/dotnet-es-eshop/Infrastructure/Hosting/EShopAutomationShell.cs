using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Idle worker for a processor host. Automation slices add reactors on the context hook.
/// </summary>
public sealed class EShopAutomationShell : BackgroundService
{
    private readonly ILogger<EShopAutomationShell> _logger;

    public EShopAutomationShell(string name, ILogger<EShopAutomationShell> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Automation shell {Name} is idle until a slice registers work.",
            Name);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

public static class EShopAutomationShellExtensions
{
    public static IServiceCollection AddEShopAutomationShell(this IServiceCollection services, string name)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        services.AddSingleton<IHostedService>(sp =>
            new EShopAutomationShell(name, sp.GetRequiredService<ILogger<EShopAutomationShell>>()));
        return services;
    }
}
