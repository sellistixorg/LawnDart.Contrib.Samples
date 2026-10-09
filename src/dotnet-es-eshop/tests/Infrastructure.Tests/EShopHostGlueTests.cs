using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LawnDart.Samples.EShop.Infrastructure.Tests;

public class EShopHostGlueTests
{
    [Fact]
    public void Identity_demo_auth_reads_callback_urls_and_issues_a_stand_in_token()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BasketApiClient"] = "http://basket",
            ["OrderingApiClient"] = "http://ordering",
            ["WebhooksApiClient"] = "http://webhooks-api",
            ["WebhooksWebClient"] = "http://webhooks-web",
            ["WebAppClient"] = "http://webapp"
        }).Build();

        var services = new ServiceCollection();
        services.AddIdentityDemoAuth(configuration);
        using var provider = services.BuildServiceProvider();
        var callbacks = provider.GetRequiredService<IdentityCallbackOptions>();
        var token = IdentityDemoToken.Create();

        Assert.Equal("http://basket", callbacks.BasketApiClient);
        Assert.Equal("http://ordering", callbacks.OrderingApiClient);
        Assert.Equal("http://webhooks-api", callbacks.WebhooksApiClient);
        Assert.Equal("http://webhooks-web", callbacks.WebhooksWebClient);
        Assert.Equal("http://webapp", callbacks.WebAppClient);
        Assert.Equal("demo", token.TokenType);
        Assert.Equal("demo-token", token.AccessToken);
    }

    [Fact]
    public void Identity_url_registers_an_absolute_http_client()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Url"] = "http://identity.example"
        }).Build();

        services.AddEShopIdentityUrl(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<EShopIdentityClientOptions>>().Value;
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(EShopIdentityClientOptions.HttpClientName);

        Assert.Equal("http://identity.example", options.Url);
        Assert.Equal(new Uri("http://identity.example"), client.BaseAddress);
    }

    [Fact]
    public void Storefront_clients_target_catalog_basket_and_ordering()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IdentityUrl"] = "http://identity.example",
            ["CallBackUrl"] = "http://webapp/callback"
        }).Build();

        services.AddEShopStorefront(configuration);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var links = provider.GetRequiredService<EShopStorefrontLinks>();

        Assert.Equal(EShopStorefrontClients.CatalogAddress, factory.CreateClient(EShopStorefrontClients.Catalog).BaseAddress);
        Assert.Equal(EShopStorefrontClients.BasketAddress, factory.CreateClient(EShopStorefrontClients.Basket).BaseAddress);
        Assert.Equal(EShopStorefrontClients.OrderingAddress, factory.CreateClient(EShopStorefrontClients.Ordering).BaseAddress);
        Assert.Equal("https+http://catalog-api", EShopStorefrontClients.CatalogAddress.OriginalString);
        Assert.Equal("http://identity.example", links.IdentityUrl);
        Assert.Equal("http://webapp/callback", links.CallBackUrl);
        Assert.Equal(new Uri("http://identity.example"), factory.CreateClient(EShopStorefrontClients.Identity).BaseAddress);
    }

    [Fact]
    public void Automation_shell_is_registered_for_a_processor_name()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEShopAutomationShell(EShopServiceNames.OrderProcessor);
        using var provider = services.BuildServiceProvider();

        var shell = provider.GetServices<IHostedService>().OfType<EShopAutomationShell>().Single();

        Assert.Equal(EShopServiceNames.OrderProcessor, shell.Name);
    }
}
