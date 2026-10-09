namespace LawnDart.Samples.EShop.WebApp.Placeholders;

/// <summary>
/// View routes on the owning service. The response DTO name is the slice name plus <c>View</c>.
/// </summary>
public static class ViewEndpoints
{
    public const string BrowseCatalog = "/views/browse-catalog";

    public const string ViewCatalogItem = "/views/view-catalog-item";

    public const string SearchCatalog = "/views/search-catalog";

    public const string BasketContents = "/views/basket-contents";

    public const string OrdersByShopper = "/views/orders-by-shopper";

    public const string OrderSummary = "/views/order-summary";

    public const string ShopperAccountProfile = "/views/shopper-account-profile";

    public const string CatalogBrandsAndTypes = "/views/catalog-brands-and-types";
}
