using LawnDart.Samples.EShop.Identity.API.Login;
using LawnDart.Samples.EShop.Infrastructure;

namespace LawnDart.Samples.EShop.Database.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class SchemaCheckTests
{
    private readonly DatabaseFixture _database;

    public SchemaCheckTests(DatabaseFixture database) => _database = database;

    [DatabaseFact]
    public async Task Empty_database_verification_fails_and_creates_no_tables()
    {
        var connectionString = await _database.CreateDatabaseAsync("empty");
        using var host = DatabaseHosts.Catalog(_database.Database, connectionString);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => host.VerifyEShopLawnDartSchemaAsync());

        Assert.Contains("not ready", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--setup-schema", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Startup does not create tables", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("column ", await SchemaSnapshot.ReadAsync(_database.Database, connectionString), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task Verification_passes_after_setup()
    {
        var connectionString = await _database.CreateDatabaseAsync("verify_ok");
        using var host = DatabaseHosts.Catalog(_database.Database, connectionString);

        await host.InitializeEShopLawnDartSchemaAsync();
        await host.VerifyEShopLawnDartSchemaAsync();
    }

    [DatabaseFact]
    public async Task Empty_login_database_verification_fails_and_creates_no_tables()
    {
        var connectionString = await _database.CreateDatabaseAsync("empty_login");
        using var host = DatabaseHosts.IdentityLogin(_database.Database, connectionString);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => host.VerifyShopperLoginStoreAsync());

        Assert.Contains("not ready", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AspNetUsers", ex.Message, StringComparison.Ordinal);
        Assert.Contains("ShopperSigningKeys", ex.Message, StringComparison.Ordinal);
        Assert.Contains("--setup-schema", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("column ", await SchemaSnapshot.ReadAsync(_database.Database, connectionString), StringComparison.Ordinal);
    }
}
