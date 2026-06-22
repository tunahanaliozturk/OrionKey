namespace Moongazing.OrionKey.EntityFrameworkCore.Tests;

using Moongazing.OrionKey;

// One id per underlying primitive so the round-trip and convention tests exercise every value shape
// the converter factory has to build (Guid, long, string).
[OrionId<System.Guid>]
public readonly partial struct OrderId;

[OrionId<long, Snowflake>]
public readonly partial struct CustomerId;

[OrionId<string, Ulid>]
public readonly partial struct TenantId;
