using LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Names the AppHost uses for the database container and for the database choice it passes to each service.
/// </summary>
public static class EShopDatabaseEnvironment
{
    /// <summary>
    /// Environment variable that carries <see cref="EShopDatabaseSetting.ConfigurationKey"/> to each service.
    /// </summary>
    public static readonly string VariableName = EShopDatabaseSetting.ConfigurationKey.Replace(":", "__", StringComparison.Ordinal);

    /// <summary>Resource name of the SQL Server container.</summary>
    public const string SqlServerResourceName = "sql";

    /// <summary>Resource name of the Postgres container.</summary>
    public const string PostgresResourceName = "postgres";

    /// <summary>Pinned Postgres image tag.</summary>
    public const string PostgresImageTag = "15.15";
}
