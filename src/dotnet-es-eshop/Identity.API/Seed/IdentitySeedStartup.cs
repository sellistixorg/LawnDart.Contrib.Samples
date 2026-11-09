using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LawnDart.Samples.EShop.Identity.API.Seed;

/// <summary>
/// Runs <see cref="IdentitySeeder"/> after the host starts.
/// The setup command does not start the host, and this service does no work when that command marked the configuration.
/// </summary>
public sealed class IdentitySeedStartup : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Creates the startup task.
    /// </summary>
    /// <param name="scopes">Scope factory. The seeder and its handlers are scoped.</param>
    /// <param name="configuration">Host configuration. Setup mode sets <see cref="EShopSchemaSetup.ConfigurationKey"/>.</param>
    public IdentitySeedStartup(IServiceScopeFactory scopes, IConfiguration configuration)
    {
        _scopes = scopes;
        _configuration = configuration;
    }

    /// <summary>
    /// Seeds Alice and Bob. Setup mode returns without seeding.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the seed.</param>
    /// <returns>A task that completes when the seed has finished or been skipped.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (EShopSchemaSetup.IsConfigured(_configuration))
            return;

        await using var scope = _scopes.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Nothing to stop. The seed does not stay running.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel shutdown.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Registers the identity seed on the host.
/// </summary>
public static class IdentitySeedRegistration
{
    /// <summary>
    /// Binds <see cref="EShopSeedOptions"/> and registers the identity seeder.
    /// </summary>
    /// <typeparam name="TBuilder">Host application builder.</typeparam>
    /// <param name="builder">Identity service host.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder AddIdentitySeed<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Configure<EShopSeedOptions>(
            builder.Configuration.GetSection(EShopSeedOptions.SectionName));
        builder.Services.AddScoped<IdentitySeeder>();
        builder.Services.AddHostedService<IdentitySeedStartup>();
        return builder;
    }
}
