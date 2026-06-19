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

failures += MonotonicHexIsOrdinalSorted();

Console.WriteLine($"AOT sample completed with {failures} failure(s).");
return failures;

// MonotonicHex ids are time-ordered and strictly increasing within a process, so a freshly
// minted run sorts identically by creation order and by ordinal string comparison. Both the
// TraceId wrapper (string storage) and the raw OrionKey.NewMonotonicHex() facade are checked.
static int MonotonicHexIsOrdinalSorted()
{
    const int count = 1000;

    var wrapped = new string[count];
    for (var i = 0; i < count; i++)
    {
        wrapped[i] = TraceId.New().Value;
    }

    var raw = new string[count];
    for (var i = 0; i < count; i++)
    {
        raw[i] = OrionKey.NewMonotonicHex();
    }

    var failures = 0;
    for (var i = 1; i < count; i++)
    {
        if (string.CompareOrdinal(wrapped[i - 1], wrapped[i]) >= 0)
        {
            Console.Error.WriteLine($"MonotonicHex (TraceId) not strictly increasing at {i}: {wrapped[i - 1]} >= {wrapped[i]}");
            failures++;
            break;
        }
    }
    for (var i = 1; i < count; i++)
    {
        if (string.CompareOrdinal(raw[i - 1], raw[i]) >= 0)
        {
            Console.Error.WriteLine($"MonotonicHex (facade) not strictly increasing at {i}: {raw[i - 1]} >= {raw[i]}");
            failures++;
            break;
        }
    }

    if (failures == 0)
    {
        Console.WriteLine($"  Mono  MonotonicHex {count} ids strictly ordinal-increasing (wrapper + facade)");
    }
    return failures;
}

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

