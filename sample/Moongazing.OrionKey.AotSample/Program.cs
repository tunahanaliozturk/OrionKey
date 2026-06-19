using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Moongazing.OrionKey;
using Moongazing.OrionKey.AotSample;

OrionKey.Configure(o => o.SnowflakeWorkerId = 1);

var options = new JsonSerializerOptions { WriteIndented = false };
// One call wires every generated id converter into the source-gen context's options - the
// reflection-free aggregate registrar emitted by OrionKey, NativeAOT-safe.
OrionKeyJsonRegistrar.AddTo(options);
var ctx = new SampleJsonContext(options);

var failures = 0;

failures += RoundTripJson(OrderId.New(), ctx.OrderId);
failures += RoundTripJson(AuditId.New(), ctx.AuditId);
failures += RoundTripJson(InvoiceId.New(), ctx.InvoiceId);
failures += RoundTripJson(UserId.New(), ctx.UserId);
failures += RoundTripJson(TenantId.New(), ctx.TenantId);
failures += RoundTripJson(SessionId.New(), ctx.SessionId);
failures += RoundTripJson(AccountId.New(), ctx.AccountId);
failures += RoundTripJson(EventId.New(), ctx.EventId);
failures += RoundTripJson(DocumentId.New(), ctx.DocumentId);
failures += RoundTripJson(TraceId.New(), ctx.TraceId);

failures += RoundTripParse(OrderId.New());
failures += RoundTripParse(UserId.New());
failures += RoundTripParse(TenantId.New());
failures += RoundTripParse(InvoiceId.New());
failures += RoundTripParse(TraceId.New());

Console.WriteLine($"AOT sample completed with {failures} failure(s).");
return failures;

static int RoundTripJson<T>(T id, JsonTypeInfo<T> typeInfo) where T : IEquatable<T>
{
    var json = JsonSerializer.Serialize(id, typeInfo);
    var restored = JsonSerializer.Deserialize(json, typeInfo);
    if (restored is null || !id.Equals(restored))
    {
        Console.Error.WriteLine($"JSON round-trip failed for {typeof(T).Name}: {id} -> {json} -> {restored}");
        return 1;
    }
    Console.WriteLine($"  STJ  {typeof(T).Name,-11} {id}");
    return 0;
}

static int RoundTripParse<T>(T id) where T : IEquatable<T>, IParsable<T>
{
    var text = id.ToString() ?? string.Empty;
    var restored = T.Parse(text, null);
    if (!id.Equals(restored))
    {
        Console.Error.WriteLine($"Parse round-trip failed for {typeof(T).Name}: {id} -> {text} -> {restored}");
        return 1;
    }
    Console.WriteLine($"  Parse {typeof(T).Name,-11} {id}");
    return 0;
}

