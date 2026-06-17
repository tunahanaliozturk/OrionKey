using System.Text.Json;

namespace Moongazing.OrionKey.Demo;

/// <summary>
/// The generator attaches a [JsonConverter] to each id, so System.Text.Json serializes it
/// as its underlying value (a bare string / number), not as a wrapper object. Deserialization
/// reconstructs the strongly-typed id, and the round-trip is value-equal.
/// </summary>
internal static class JsonDemo
{
    private sealed record OrderDto(OrderId Order, UserId User, TenantId Tenant);

    public static void Run()
    {
        DemoConsole.Section("6. System.Text.Json serialization");

        var dto = new OrderDto(OrderId.New(), UserId.New(), TenantId.New());

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(dto, options);

        Console.WriteLine(json);

        var roundTrip = JsonSerializer.Deserialize<OrderDto>(json, options)!;
        DemoConsole.Item("Order round-trip equal", dto.Order == roundTrip.Order);
        DemoConsole.Item("User round-trip equal", dto.User == roundTrip.User);
        DemoConsole.Item("Tenant round-trip equal", dto.Tenant == roundTrip.Tenant);

        DemoConsole.Note("Each id serializes as its raw value, not as a nested { \"Value\": ... } object.");
    }
}
