using LawnDart.Samples.EShop.Catalog.API.Seed;
using LawnDart.Samples.EShop.Identity.API.Seed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

public class EShopSchemaSetupTests
{
    [Theory]
    [InlineData("--setup-schema")]
    [InlineData("--Setup-Schema")]
    [InlineData(" --setup-schema ")]
    public void Setup_argument_is_recognized(string arg)
    {
        Assert.True(EShopSchemaSetup.IsRequested([arg]));
    }

    [Fact]
    public void Running_the_service_is_not_the_setup_command()
    {
        Assert.False(EShopSchemaSetup.IsRequested([]));
        Assert.False(EShopSchemaSetup.IsRequested(["--database", "postgres"]));
        Assert.False(EShopSchemaSetup.IsRequested(null));
    }

    [Fact]
    public async Task Catalog_seeder_does_not_run_in_setup_mode()
    {
        using var host = Build(services => services.AddHostedService<CatalogSeedStartup>());
        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task Identity_seeder_does_not_run_in_setup_mode()
    {
        using var host = Build(services => services.AddHostedService<IdentitySeedStartup>());
        await host.StartAsync();
        await host.StopAsync();
    }

    private static IHost Build(Action<IServiceCollection> register)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.Mark();
        register(builder.Services);
        return builder.Build();
    }
}
