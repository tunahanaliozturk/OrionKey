using Moongazing.OrionKey;

namespace Moongazing.OrionKey.AotSample;

[OrionId<System.Guid>]                  public readonly partial struct OrderId;
[OrionId<System.Guid, GuidV7>]          public readonly partial struct AuditId;
[OrionId<System.Guid, SequentialGuid>]  public readonly partial struct InvoiceId;
[OrionId<long, Snowflake>]              public readonly partial struct UserId;
[OrionId<string, Ulid>]                 public readonly partial struct TenantId;
[OrionId<string, NanoId>]               public readonly partial struct SessionId;
[OrionId<string, Cuid2>]               public readonly partial struct AccountId;
[OrionId<string, Ksuid>]               public readonly partial struct EventId;
[OrionId<string, ObjectId>]            public readonly partial struct DocumentId;
[OrionId<string, MonotonicHex>]        public readonly partial struct TraceId;
