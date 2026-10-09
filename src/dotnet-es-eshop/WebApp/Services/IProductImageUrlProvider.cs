namespace LawnDart.Samples.EShop.WebApp.Services;

public interface IProductImageUrlProvider
{
    /// <summary>
    /// Picture URL for a catalog item id. The address is <c>product-images/{productId}</c>.
    /// </summary>
    /// <param name="productId">Item id.</param>
    /// <returns>The image address.</returns>
    string GetProductImageUrl(Guid productId);

    /// <summary>
    /// Picture URL for a catalog item detail. A rooted URL is used as given. A file name uses the example picture.
    /// </summary>
    /// <param name="pictureUrl">Picture URL.</param>
    /// <returns>The image address for the item page.</returns>
    string GetProductImageUrl(string? pictureUrl);

    /// <summary>
    /// Picture URL for one catalog item.
    /// A rooted URL or an absolute URL is used as given.
    /// A file name uses <c>product-images/{productId}</c>. The WebApp forwards that path to Catalog.
    /// An empty name uses the example picture.
    /// </summary>
    /// <param name="productId">Item id.</param>
    /// <param name="pictureUrl">Picture file name or URL from the catalog.</param>
    /// <returns>The image address.</returns>
    string GetProductImageUrl(Guid productId, string? pictureUrl);
}
