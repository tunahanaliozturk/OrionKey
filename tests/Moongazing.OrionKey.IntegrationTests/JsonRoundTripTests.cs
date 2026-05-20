using System.Text.Json;

namespace Moongazing.OrionKey.IntegrationTests;

public class JsonRoundTripTests
{
    [Fact]
    public void OrderId_ShouldRoundTripThroughJson()
    {
        var original = OrderId.New();
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<OrderId>(json);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void UserId_ShouldSerializeAsRawNumber()
    {
        var id = new UserId(123456789);
        Assert.Equal("123456789", JsonSerializer.Serialize(id));
    }

    [Fact]
    public void TenantId_ShouldSerializeAsRawString()
    {
        var id = new TenantId("01HZY0000000000000000000AB");
        Assert.Equal("\"01HZY0000000000000000000AB\"", JsonSerializer.Serialize(id));
    }
}
