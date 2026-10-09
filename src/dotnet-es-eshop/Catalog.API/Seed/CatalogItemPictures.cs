using LawnDart.Aggregates;
using LawnDart.Samples.EShop.Catalog.API.Aggregates.CatalogItem;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LawnDart.Samples.EShop.Catalog.API.Seed;

/// <summary>
/// Serves a catalog item picture from the seed Pics folder.
/// The route is <c>GET /api/catalog/items/{id}/pic</c>.
/// </summary>
public static class CatalogItemPictures
{
    /// <summary>
    /// Picture route. <c>id</c> is the catalog item id.
    /// </summary>
    public const string Route = "/api/catalog/items/{id:guid}/pic";

    /// <summary>
    /// Maps the picture route.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapCatalogItemPictures(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(Route, GetAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IAggregateRepository items,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return Results.NotFound();

        var item = await items.GetOrCreateAsync<CatalogItem>(id, cancellationToken).ConfigureAwait(false);
        var picture = item.State.PictureFileName;
        if (!item.State.Exists || string.IsNullOrWhiteSpace(picture))
            return Results.NotFound();

        var fileName = Path.GetFileName(picture);
        if (!string.Equals(fileName, picture, StringComparison.Ordinal))
            return Results.NotFound();

        var directory = Path.GetFullPath(CatalogSeedFiles.PicsDirectory(environment));
        var root = directory.EndsWith(Path.DirectorySeparatorChar)
            ? directory
            : directory + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(directory, fileName));
        if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
            return Results.NotFound();

        var extension = Path.GetExtension(fileName);
        return Results.File(
            File.OpenRead(path),
            CatalogSeedFiles.MimeType(extension),
            lastModified: File.GetLastWriteTimeUtc(path),
            enableRangeProcessing: true);
    }
}
