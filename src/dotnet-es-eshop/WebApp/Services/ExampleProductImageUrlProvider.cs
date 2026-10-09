using LawnDart.Samples.EShop.WebApp.Placeholders;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Picture URL for a catalog file name. A rooted URL is used as given. A file name uses the catalog picture route.
/// </summary>
public sealed class ExampleProductImageUrlProvider : IProductImageUrlProvider
{
    /// <inheritdoc />
    public string GetProductImageUrl(Guid productId)
        => $"product-images/{productId:D}";

    /// <inheritdoc />
    public string GetProductImageUrl(string? pictureUrl)
        => string.IsNullOrWhiteSpace(pictureUrl)
            || (!pictureUrl.StartsWith('/') && !pictureUrl.Contains("://", StringComparison.Ordinal))
            ? ExampleStore.PictureUrl
            : pictureUrl;

    /// <inheritdoc />
    public string GetProductImageUrl(Guid productId, string? pictureUrl)
    {
        if (string.IsNullOrWhiteSpace(pictureUrl))
            return ExampleStore.PictureUrl;

        if (pictureUrl.StartsWith('/') || pictureUrl.Contains("://", StringComparison.Ordinal))
            return pictureUrl;

        return GetProductImageUrl(productId);
    }
}
