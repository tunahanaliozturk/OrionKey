using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Moongazing.OrionKey.IntegrationTests;

public class OpenApiSchemaTests
{
    private static SchemaFilterContext ContextFor(System.Type t)
        => new(t, schemaGenerator: null!, schemaRepository: new SchemaRepository());

    [Fact]
    public void OrderId_Guid_BecomesStringUuid()
    {
        var schema = new OpenApiSchema { Type = "object" };
        new OrderIdSchemaFilter().Apply(schema, ContextFor(typeof(OrderId)));
        Assert.Equal("string", schema.Type);
        Assert.Equal("uuid", schema.Format);
    }

    [Fact]
    public void UserId_Snowflake_BecomesIntegerInt64()
    {
        var schema = new OpenApiSchema { Type = "object" };
        new UserIdSchemaFilter().Apply(schema, ContextFor(typeof(UserId)));
        Assert.Equal("integer", schema.Type);
        Assert.Equal("int64", schema.Format);
    }

    [Fact]
    public void TenantId_Ulid_BecomesStringWithoutFormat()
    {
        var schema = new OpenApiSchema { Type = "object" };
        new TenantIdSchemaFilter().Apply(schema, ContextFor(typeof(TenantId)));
        Assert.Equal("string", schema.Type);
        Assert.Null(schema.Format);
    }

    [Fact]
    public void Filter_IgnoresUnrelatedTypes()
    {
        var schema = new OpenApiSchema { Type = "object", Format = "preserved" };
        new OrderIdSchemaFilter().Apply(schema, ContextFor(typeof(string)));
        Assert.Equal("object", schema.Type);
        Assert.Equal("preserved", schema.Format);
    }
}
