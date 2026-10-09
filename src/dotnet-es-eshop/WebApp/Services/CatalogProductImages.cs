using System.Net;
using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Forwards <c>GET /product-images/{id}</c> to Catalog <c>GET /api/catalog/items/{id}/pic</c>.
/// </summary>
public static class CatalogProductImages
{
    /// <summary>
    /// Browser route for a catalog item picture.
    /// </summary>
    public const string Route = "/product-images/{id:guid}";

    /// <summary>
    /// Maps the picture forwarder.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same application.</returns>
    public static WebApplication MapCatalogProductImages(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet(Route, GetAsync);
        return app;
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(EShopStorefrontClients.Catalog);
        using var response = await client
            .GetAsync($"/api/catalog/items/{id:D}/pic", cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return Results.NotFound();

        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return Results.File(bytes, contentType);
    }
}
