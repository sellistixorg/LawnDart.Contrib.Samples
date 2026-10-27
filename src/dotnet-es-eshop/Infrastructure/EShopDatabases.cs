namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Aspire database names from <c>AppHost</c>.
/// Each name is the <c>ConnectionStrings</c> key injected by <c>WithReference</c>.
/// The names are the same on SQL Server and Postgres.
/// </summary>
public static class EShopDatabases
{
    /// <summary>
    /// LawnDart event log for Identity.
    /// </summary>
    public const string Identity = "identitydb";

    /// <summary>
    /// ASP.NET Core Identity login store for Identity.
    /// Separate from <see cref="Identity"/> so login schema setup does not share the event log tables.
    /// </summary>
    public const string IdentityLogin = "identitylogindb";

    public const string Catalog = "catalogdb";

    public const string Basket = "basketdb";

    public const string Ordering = "orderingdb";

    public const string Payment = "paymentdb";

    public const string Webhooks = "webhooksdb";
}
