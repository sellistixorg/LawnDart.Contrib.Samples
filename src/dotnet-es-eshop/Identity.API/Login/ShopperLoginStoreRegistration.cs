using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LawnDart.Samples.EShop.Identity.API.Login;

/// <summary>
/// Registers the Identity login store on <c>identitylogindb</c>.
/// </summary>
public static class ShopperLoginStoreRegistration
{
    /// <summary>
    /// Registers <see cref="ApplicationDbContext"/>, ASP.NET Core Identity, and <see cref="IShopperLoginStore"/>.
    /// </summary>
    /// <remarks>
    /// The <c>EShop:Database</c> setting picks the database, the same as for the LawnDart stores.
    /// <c>SqlServer</c> (the default) uses the SQL Server provider. <c>Postgres</c> uses the Npgsql provider.
    /// The login tables use the same names on both databases.
    /// </remarks>
    /// <param name="builder">Identity service host.</param>
    /// <typeparam name="TBuilder">Host application builder.</typeparam>
    /// <returns>The same builder.</returns>
    public static TBuilder AddShopperLoginStore<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var connectionString = builder.Configuration.GetConnectionString(EShopDatabases.IdentityLogin);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{EShopDatabases.IdentityLogin}' is missing. " +
                "Start this service from the Aspire AppHost so the database reference is injected.");
        }

        var database = builder.Configuration.ReadEShopDatabase();
        if (database == EShopDatabase.Postgres)
        {
            builder.AddNpgsqlDbContext<ApplicationDbContext>(
                EShopDatabases.IdentityLogin,
                configureDbContextOptions: options => options.EnableSensitiveDataLogging(false));
        }
        else
        {
            builder.AddSqlServerDbContext<ApplicationDbContext>(
                EShopDatabases.IdentityLogin,
                configureDbContextOptions: options => options.EnableSensitiveDataLogging(false));
        }

        builder.Services
            .AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        builder.Services.AddScoped<IShopperLoginStore, ShopperLoginStore>();
        return builder;
    }

    /// <summary>
    /// Creates the Identity tables when they are missing.
    /// </summary>
    /// <param name="host">Built Identity host.</param>
    /// <param name="cancellationToken">Token used to cancel schema creation.</param>
    /// <returns>A task that completes when the login schema is ready.</returns>
    /// <remarks>
    /// LawnDart creates the event log with <c>InitializeSchemaAsync</c>.
    /// This method calls <c>Database.EnsureCreatedAsync</c> on <c>identitylogindb</c>.
    /// <c>EnsureCreated</c> skips schema creation when the database already has tables, and <c>identitydb</c> already holds the event log.
    /// After that, each missing nullable card column is added to <c>AspNetUsers</c>.
    /// A second call leaves those columns as they are.
    /// The shopper signing key table is created the same way, and one RSA key row is inserted when the table is empty.
    /// </remarks>
    public static async Task InitializeShopperLoginStoreAsync(
        this IHost host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        await ShopperLoginCardColumns.AddMissingAsync(db, cancellationToken).ConfigureAwait(false);
        await ShopperSigningKeySchema.AddMissingAsync(db, cancellationToken).ConfigureAwait(false);
        await PublishValidationKeysAsync(host, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the login tables and stops when they are missing.
    /// Then loads the stored signing keys into <see cref="ShopperValidationKeys"/>.
    /// Does not create tables.
    /// </summary>
    /// <remarks>
    /// The setup command (<c>--setup-schema</c>) runs in its own process, so the keys it publishes
    /// are gone when the service starts. Without this load, Identity rejects its own access tokens
    /// on bearer routes such as the shopper account profile. The setup command inserts the key row,
    /// so this load normally only reads it.
    /// </remarks>
    /// <param name="host">Built Identity host.</param>
    /// <param name="cancellationToken">Token used to cancel the check.</param>
    public static async Task VerifyShopperLoginStoreAsync(
        this IHost host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var missing = await ShopperLoginSchemaCheck.FindMissingAsync(db, cancellationToken).ConfigureAwait(false);
        if (missing.Count == 0)
        {
            await PublishValidationKeysAsync(host, cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException(
            "The login store schema is not ready: missing " + string.Join(", ", missing) + ". " +
            "Run this service with " + EShopSchemaSetup.Argument + " before starting it. " +
            "Startup does not create tables.");
    }

    private static async Task PublishValidationKeysAsync(IHost host, CancellationToken cancellationToken)
    {
        if (host.Services.GetService<ShopperSigningKeyRing>() is ShopperSigningKeyRing ring
            && host.Services.GetService<ShopperValidationKeys>() is ShopperValidationKeys validation)
        {
            await ring.PublishAsync(validation, cancellationToken).ConfigureAwait(false);
        }
    }
}
