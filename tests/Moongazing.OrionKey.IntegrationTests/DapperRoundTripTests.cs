using Dapper;
using Microsoft.Data.Sqlite;

namespace Moongazing.OrionKey.IntegrationTests;

[Collection(DapperStaticStateCollectionMarker.Name)]
public class DapperRoundTripTests
{
    [Fact]
    public async Task OrderId_Guid_RoundTripsThroughDapper()
    {
        SqlMapper.AddTypeHandler(new OrderIdDapperTypeHandler());

        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await connection.ExecuteAsync("CREATE TABLE orders (id TEXT PRIMARY KEY, name TEXT)");

        var id = OrderId.New();
        await connection.ExecuteAsync(
            "INSERT INTO orders (id, name) VALUES (@id, @name)",
            new { id, name = "Widget" });

        var loaded = await connection.QuerySingleAsync<OrderId>(
            "SELECT id FROM orders WHERE name = @name",
            new { name = "Widget" });

        Assert.Equal(id, loaded);
    }

    [Fact]
    public async Task TenantId_Ulid_RoundTripsThroughDapper()
    {
        SqlMapper.AddTypeHandler(new TenantIdDapperTypeHandler());

        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await connection.ExecuteAsync("CREATE TABLE tenants (id TEXT PRIMARY KEY)");

        var id = TenantId.New();
        await connection.ExecuteAsync("INSERT INTO tenants (id) VALUES (@id)", new { id });
        var loaded = await connection.QuerySingleAsync<TenantId>("SELECT id FROM tenants");

        Assert.Equal(id, loaded);
    }
}
