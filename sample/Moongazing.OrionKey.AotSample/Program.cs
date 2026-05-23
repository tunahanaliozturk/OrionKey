using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Moongazing.OrionKey;
using Moongazing.OrionKey.AotSample;

OrionKey.Configure(o => o.SnowflakeWorkerId = 1);

var failures = 0;

failures += RoundTripJson(OrderId.New(), SampleJsonContext.Default.OrderId);
failures += RoundTripJson(AuditId.New(), SampleJsonContext.Default.AuditId);
failures += RoundTripJson(InvoiceId.New(), SampleJsonContext.Default.InvoiceId);
failures += RoundTripJson(UserId.New(), SampleJsonContext.Default.UserId);
failures += RoundTripJson(TenantId.New(), SampleJsonContext.Default.TenantId);
failures += RoundTripJson(SessionId.New(), SampleJsonContext.Default.SessionId);
failures += RoundTripJson(AccountId.New(), SampleJsonContext.Default.AccountId);
failures += RoundTripJson(EventId.New(), SampleJsonContext.Default.EventId);
failures += RoundTripJson(DocumentId.New(), SampleJsonContext.Default.DocumentId);

failures += RoundTripParse(OrderId.New());
failures += RoundTripParse(UserId.New());
failures += RoundTripParse(TenantId.New());
failures += RoundTripParse(InvoiceId.New());

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
