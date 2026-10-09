using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LawnDart.Samples.EShop.Identity.API.Seed;

/// <summary>
/// Runs <see cref="IdentitySeeder"/> after the host starts.
/// Schema initialization in Program.cs finishes before the host runs this service.
/// </summary>
public sealed class IdentitySeedStartup : IHostedService
{
    private readonly IServiceScopeFactory _scopes;

    /// <summary>
    /// Creates the startup task.
    /// </summary>
    /// <param name="scopes">Scope factory. The seeder and its handlers are scoped.</param>
    public IdentitySeedStartup(IServiceScopeFactory scopes) => _scopes = scopes;

    /// <summary>
    /// Seeds Alice and Bob.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the seed.</param>
    /// <returns>A task that completes when the seed has finished or been skipped.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
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
