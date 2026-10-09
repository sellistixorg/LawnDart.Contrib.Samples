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
    /// <param name="builder">Identity service host.</param>
    /// <typeparam name="TBuilder">Host application builder.</typeparam>
    /// <returns>The same builder.</returns>
    public static TBuilder AddShopperLoginStore<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var connectionString = builder.Configuration.GetConnectionString(EShopSqlConnections.IdentityLogin);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{EShopSqlConnections.IdentityLogin}' is missing. " +
                "Start this service from the Aspire AppHost so the SQL database reference is injected.");
        }

        builder.AddSqlServerDbContext<ApplicationDbContext>(
            EShopSqlConnections.IdentityLogin,
            configureDbContextOptions: options => options.EnableSensitiveDataLogging(false));

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
    /// LawnDart creates the event log with <c>InitializeSchemaAsync</c>, not a versioned migrator.
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
        if (host.Services.GetService<ShopperSigningKeyRing>() is ShopperSigningKeyRing ring
            && host.Services.GetService<ShopperValidationKeys>() is ShopperValidationKeys validation)
        {
            await ring.PublishAsync(validation, cancellationToken).ConfigureAwait(false);
        }
    }
}
