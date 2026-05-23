using Newtonsoft.Json;

namespace Moongazing.OrionKey.IntegrationTests;

public class NewtonsoftJsonRoundTripTests
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Converters =
        {
            new OrderIdNewtonsoftJsonConverter(),
            new UserIdNewtonsoftJsonConverter(),
            new TenantIdNewtonsoftJsonConverter(),
        },
    };

    [Fact]
    public void OrderId_Guid_RoundTrips()
    {
        var id = OrderId.New();
        var json = JsonConvert.SerializeObject(id, Settings);
        var restored = JsonConvert.DeserializeObject<OrderId>(json, Settings);
        Assert.Equal(id, restored);
    }

    [Fact]
    public void UserId_Snowflake_RoundTrips()
    {
        var id = new UserId(123456789);
        var json = JsonConvert.SerializeObject(id, Settings);
        Assert.Equal("123456789", json);
        var restored = JsonConvert.DeserializeObject<UserId>(json, Settings);
        Assert.Equal(id, restored);
    }

    [Fact]
    public void TenantId_Ulid_RoundTripsAsString()
    {
        var id = TenantId.New();
        var json = JsonConvert.SerializeObject(id, Settings);
        Assert.StartsWith("\"", json);
        var restored = JsonConvert.DeserializeObject<TenantId>(json, Settings);
        Assert.Equal(id, restored);
    }
}
