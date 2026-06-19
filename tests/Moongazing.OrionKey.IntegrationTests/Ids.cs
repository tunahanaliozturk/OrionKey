using Moongazing.OrionKey;

namespace Moongazing.OrionKey.IntegrationTests;

[OrionId<System.Guid>]              public readonly partial struct OrderId;
[OrionId<long, Snowflake>]          public readonly partial struct UserId;
[OrionId<string, Ulid>]             public readonly partial struct TenantId;
[OrionId<System.Guid, GuidV7>]      public readonly partial struct AuditId;
[OrionId<string, Cuid2>]            public readonly partial struct AccountId;
[OrionId<string, Ksuid>]            public readonly partial struct EventId;
[OrionId<string, ObjectId>]         public readonly partial struct DocumentId;
[OrionId<System.Guid, SequentialGuid>] public readonly partial struct InvoiceId;
[OrionId<string, MonotonicHex>]     public readonly partial struct TraceId;
