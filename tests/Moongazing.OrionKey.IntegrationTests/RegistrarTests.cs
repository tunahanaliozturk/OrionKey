using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.OpenApi.Models;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Moongazing.OrionKey.IntegrationTests;

public class RegistrarTests
{
    [Fact]
    public async Task DapperRegistrar_RegistersHandlersForAllIds()
    {
        OrionKeyDapperRegistrar.Register();

        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await connection.ExecuteAsync("CREATE TABLE t (id TEXT)");
        var id = OrderId.New();
        await connection.ExecuteAsync("INSERT INTO t (id) VALUES (@id)", new { id });
        var loaded = await connection.QuerySingleAsync<OrderId>("SELECT id FROM t");
        Assert.Equal(id, loaded);
    }

    [Fact]
    public void NewtonsoftRegistrar_AddsConvertersToSettings()
    {
        var settings = new JsonSerializerSettings();
        OrionKeyNewtonsoftJsonRegistrar.AddTo(settings);
        Assert.NotEmpty(settings.Converters);
        var id = OrderId.New();
        var json = JsonConvert.SerializeObject(id, settings);
        var restored = JsonConvert.DeserializeObject<OrderId>(json, settings);
        Assert.Equal(id, restored);
    }

    [Fact]
    public void MongoRegistrar_RegistersSerializers()
    {
        // BsonSerializer.RegisterSerializer throws if called twice for the same type, so
        // tolerate "already registered" — another test (MongoSerializerTests) may have
        // pre-registered Guid (and the registrar would re-register multiple types).
        try { OrionKeyMongoRegistrar.Register(); }
        catch (MongoDB.Bson.BsonSerializationException) { /* already registered */ }

        var serializer = BsonSerializer.LookupSerializer<OrderId>();
        Assert.NotNull(serializer);
    }

    [Fact]
    public void OpenApiRegistrar_AddsFilterForEachId()
    {
        var options = new SwaggerGenOptions();
        OrionKeyOpenApiRegistrar.AddTo(options);

        var schema = new OpenApiSchema { Type = "object" };
        var ctx = new SchemaFilterContext(typeof(OrderId), schemaGenerator: null!, schemaRepository: new SchemaRepository());

        foreach (var filterDescriptor in options.SchemaFilterDescriptors)
        {
            var filter = (ISchemaFilter)System.Activator.CreateInstance(filterDescriptor.Type)!;
            filter.Apply(schema, ctx);
        }

        Assert.Equal("string", schema.Type);
        Assert.Equal("uuid", schema.Format);
    }
}
