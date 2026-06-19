using System.Text.Json;
using System.Text.Json.Serialization;

namespace Moongazing.OrionKey.IntegrationTests;

/// <summary>
/// A DTO that nests id values, exercised through both the reflection-based serializer and the
/// System.Text.Json source-generation path (<see cref="OrderDtoContext"/>).
/// </summary>
public sealed record OrderDto(OrderId Order, UserId User, TenantId Tenant, TraceId Trace);

/// <summary>
/// Source-generated metadata for <see cref="OrderDto"/> and the id types it nests. Having this
/// context proves the generated converters work in reflection-free / NativeAOT mode: STJ resolves
/// the id members through compile-time metadata, dispatching to the generated
/// <c>JsonConverter&lt;T&gt;</c> attached via <c>[JsonConverter]</c>.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(OrderDto))]
[JsonSerializable(typeof(OrderId))]
[JsonSerializable(typeof(UserId))]
[JsonSerializable(typeof(TenantId))]
[JsonSerializable(typeof(TraceId))]
internal sealed partial class OrderDtoContext : JsonSerializerContext;

public class SourceGenJsonTests
{
    /// <summary>
    /// Builds the source-generation context over options that carry every generated id converter.
    /// This is the canonical AOT-safe wiring: a bare <c>Context.Default</c> would treat each id
    /// struct as an object (its public <c>Value</c> property) because source-gen metadata does not
    /// itself honor the struct's <c>[JsonConverter]</c>; registering the converters on the options
    /// the context is constructed with makes the context dispatch to them. The one-call
    /// <c>OrionKeyJsonRegistrar.AddTo</c> replaces a per-id <c>Converters.Add</c> block.
    /// </summary>
    private static OrderDtoContext NewSourceGenContext()
    {
        var options = new JsonSerializerOptions();
        OrionKeyJsonRegistrar.AddTo(options);
        return new OrderDtoContext(options);
    }

    [Fact]
    public void Dto_RoundTrips_ThroughSourceGenContext()
    {
        var ctx = NewSourceGenContext();
        var dto = new OrderDto(OrderId.New(), new UserId(42), TenantId.New(), TraceId.New());

        var json = JsonSerializer.Serialize(dto, ctx.OrderDto);
        var restored = JsonSerializer.Deserialize(json, ctx.OrderDto);

        Assert.Equal(dto, restored);
    }

    [Fact]
    public void SourceGen_And_Reflection_ProduceIdenticalJson_ForId()
    {
        var ctx = NewSourceGenContext();
        var trace = TraceId.New();

        var reflection = JsonSerializer.Serialize(trace);
        var sourceGen = JsonSerializer.Serialize(trace, ctx.TraceId);

        Assert.Equal(reflection, sourceGen);
    }

    [Fact]
    public void SourceGen_And_Reflection_ProduceIdenticalJson_ForDto()
    {
        var ctx = NewSourceGenContext();
        var dto = new OrderDto(OrderId.New(), new UserId(7), TenantId.New(), TraceId.New());

        var reflection = JsonSerializer.Serialize(dto);
        var sourceGen = JsonSerializer.Serialize(dto, ctx.OrderDto);

        Assert.Equal(reflection, sourceGen);
    }

    [Fact]
    public void Id_SerializesAsRawScalar_NotObject()
    {
        // The generated converter writes the underlying value directly (string/number), so an
        // id nested in a DTO is a bare scalar, not a wrapper object.
        var ctx = NewSourceGenContext();

        var user = new UserId(123456789);
        Assert.Equal("123456789", JsonSerializer.Serialize(user, ctx.UserId));

        var tenant = new TenantId("01HZY0000000000000000000AB");
        Assert.Equal("\"01HZY0000000000000000000AB\"", JsonSerializer.Serialize(tenant, ctx.TenantId));
    }

    [Fact]
    public void GeneratedConverterFactory_ResolvesEveryIdType()
    {
        var factory = new OrionKeyJsonConverterFactory();

        Assert.True(factory.CanConvert(typeof(OrderId)));
        Assert.True(factory.CanConvert(typeof(UserId)));
        Assert.True(factory.CanConvert(typeof(TraceId)));
        Assert.False(factory.CanConvert(typeof(string)));

        var options = new JsonSerializerOptions();
        var converter = factory.CreateConverter(typeof(TraceId), options);
        Assert.IsAssignableFrom<JsonConverter<TraceId>>(converter);
    }

    [Fact]
    public void GeneratedRegistrar_AddsEveryConverter_AndRoundTrips()
    {
        var options = new JsonSerializerOptions();
        OrionKeyJsonRegistrar.AddTo(options);

        var original = TraceId.New();
        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<TraceId>(json, options);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void GeneratedRegistrar_AddTo_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => OrionKeyJsonRegistrar.AddTo(null!));
}
