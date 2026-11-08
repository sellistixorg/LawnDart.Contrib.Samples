using LawnDart.Samples.EShop.Infrastructure;
using Microsoft.Extensions.Hosting;

namespace LawnDart.Samples.EShop.Database.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class SchemaSetupTests
{
    private readonly DatabaseFixture _database;

    public SchemaSetupTests(DatabaseFixture database) => _database = database;

    [DatabaseTheory]
    [InlineData("basket")]
    [InlineData("identity")]
    [InlineData("payment")]
    [InlineData("webhooks")]
    public async Task Setup_twice_leaves_the_schema_and_rows_unchanged(string service)
    {
        var connectionString = await _database.CreateDatabaseAsync(service);
        using var host = Open(service, connectionString);

        await host.InitializeEShopLawnDartSchemaAsync();
        var first = await SchemaSnapshot.ReadAsync(_database.Database, connectionString);

        await host.InitializeEShopLawnDartSchemaAsync();
        var second = await SchemaSnapshot.ReadAsync(_database.Database, connectionString);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    private IHost Open(string service, string connectionString)
    {
        var database = _database.Database;
        return service switch
        {
            "basket" => DatabaseHosts.Basket(database, connectionString),
            "identity" => DatabaseHosts.IdentityEvents(database, connectionString),
            "payment" => DatabaseHosts.Payment(database, connectionString),
            "webhooks" => DatabaseHosts.Webhooks(database, connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, null)
        };
    }
}
