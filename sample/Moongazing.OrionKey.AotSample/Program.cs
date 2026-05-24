using Dapper;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Moongazing.OrionKey;
using Moongazing.OrionKey.AotSample;

OrionKey.Configure(o => o.SnowflakeWorkerId = 1);

var options = new JsonSerializerOptions { WriteIndented = false };
options.Converters.Add(new OrderIdJsonConverter());
options.Converters.Add(new AuditIdJsonConverter());
options.Converters.Add(new InvoiceIdJsonConverter());
options.Converters.Add(new UserIdJsonConverter());
options.Converters.Add(new TenantIdJsonConverter());
options.Converters.Add(new SessionIdJsonConverter());
options.Converters.Add(new AccountIdJsonConverter());
options.Converters.Add(new EventIdJsonConverter());
options.Converters.Add(new DocumentIdJsonConverter());
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

failures += RoundTripParse(OrderId.New());
failures += RoundTripParse(UserId.New());
failures += RoundTripParse(TenantId.New());
failures += RoundTripParse(InvoiceId.New());

failures += RoundTripDapper();

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

static int RoundTripDapper()
{
    SqlMapper.AddTypeHandler(new OrderIdDapperTypeHandler());
    using var connection = new SqliteConnection("Filename=:memory:");
    connection.Open();
    connection.Execute("CREATE TABLE orders (id TEXT)");

    var id = OrderId.New();
    var p = new DynamicParameters();
    p.Add("id", id);
    connection.Execute("INSERT INTO orders (id) VALUES (@id)", p);
    var loaded = connection.QuerySingle<OrderId>("SELECT id FROM orders");

    if (!id.Equals(loaded))
    {
        Console.Error.WriteLine($"Dapper round-trip failed: {id} != {loaded}");
        return 1;
    }
    Console.WriteLine($"  Dapper OrderId     {id}");
    return 0;
}
