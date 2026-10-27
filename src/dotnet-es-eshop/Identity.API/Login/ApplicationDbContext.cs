using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LawnDart.Samples.EShop.Identity.API.Login;

/// <summary>
/// ASP.NET Core Identity database for shopper logins.
/// </summary>
public sealed class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    /// <summary>
    /// Creates the context.
    /// </summary>
    /// <param name="options">SQL Server or Postgres options for <c>identitylogindb</c>.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// RSA private keys for shopper access tokens.
    /// </summary>
    public DbSet<ShopperSigningKey> ShopperSigningKeys => Set<ShopperSigningKey>();

    /// <summary>
    /// Uses the ASP.NET Core Identity model, including the nullable card columns on <see cref="ApplicationUser"/>.
    /// </summary>
    /// <param name="builder">Model builder.</param>
    /// <remarks>
    /// <c>EnsureCreated</c> creates those columns with the login tables.
    /// It does not add columns when the database already has tables.
    /// <see cref="ShopperLoginStoreRegistration.InitializeShopperLoginStoreAsync"/> adds any card column that is still missing.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ShopperSigningKey>(entity =>
        {
            entity.ToTable("ShopperSigningKeys");
            entity.HasKey(key => key.Id);
            entity.Property(key => key.Id).HasMaxLength(64);
            entity.Property(key => key.Pkcs8PrivateKey).IsRequired();
            entity.Property(key => key.CreatedAtUtc).IsRequired();
        });
    }
}
