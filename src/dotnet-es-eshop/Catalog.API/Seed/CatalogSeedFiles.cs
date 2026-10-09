using Microsoft.AspNetCore.Hosting;

namespace LawnDart.Samples.EShop.Catalog.API.Seed;

/// <summary>
/// Locates the copied catalog pictures and maps a file extension to a media type.
/// </summary>
public static class CatalogSeedFiles
{
    /// <summary>
    /// Directory that holds the picture files.
    /// </summary>
    /// <param name="environment">Host environment.</param>
    /// <returns>The Pics directory under the content root, or under the output directory.</returns>
    public static string PicsDirectory(IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var fromContent = Path.Combine(environment.ContentRootPath, "Seed", "Pics");
        if (Directory.Exists(fromContent))
            return fromContent;

        return Path.Combine(AppContext.BaseDirectory, "Seed", "Pics");
    }

    /// <summary>
    /// Media type for a picture file extension.
    /// </summary>
    /// <param name="extension">Extension including the leading dot.</param>
    /// <returns>The media type. An unknown extension is <c>application/octet-stream</c>.</returns>
    public static string MimeType(string extension) => extension switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".bmp" => "image/bmp",
        ".tiff" => "image/tiff",
        ".wmf" => "image/wmf",
        ".jp2" => "image/jp2",
        ".svg" => "image/svg+xml",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
