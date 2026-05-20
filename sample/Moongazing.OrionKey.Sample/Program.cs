using Moongazing.OrionKey;
using Moongazing.OrionKey.Sample;

OrionKey.Configure(o => o.SnowflakeWorkerId = 1);

var order = OrderId.New();
var user = UserId.New();
var tenant = TenantId.New();
var session = SessionId.New();

Console.WriteLine($"OrderId   (Guid)      : {order}");
Console.WriteLine($"UserId    (Snowflake) : {user}");
Console.WriteLine($"TenantId  (ULID)      : {tenant}");
Console.WriteLine($"SessionId (NanoId)    : {session}");

var parsed = Guid.TryParse(order.ToString(), out var g) ? new OrderId(g) : OrderId.Empty;
Console.WriteLine($"Round-trip equal      : {order == parsed}");

var earlier = UserId.New();
var later = UserId.New();
Console.WriteLine($"Snowflake ordered     : {earlier < later}");
