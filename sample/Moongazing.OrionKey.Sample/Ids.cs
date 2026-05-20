using Moongazing.OrionKey;

namespace Moongazing.OrionKey.Sample;

[OrionId<System.Guid>]     public readonly partial struct OrderId;
[OrionId<long, Snowflake>] public readonly partial struct UserId;
[OrionId<string, Ulid>]    public readonly partial struct TenantId;
[OrionId<string, NanoId>]  public readonly partial struct SessionId;
