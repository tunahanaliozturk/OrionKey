using Moongazing.OrionKey;

namespace Moongazing.OrionKey.Demo;

// One strongly-typed id per generation strategy. The [OrionId<...>] attribute drives
// the source generator: it emits the struct body (Value, ctor, New, Empty, equality),
// IComparable for sortable strategies, parsing, formatting, and the framework converters.

// Random Guid (not sortable) -- the classic surrogate key.
[OrionId<Guid>] public readonly partial struct OrderId;

// UUIDv7: time-ordered Guid, index-friendly.
[OrionId<Guid, GuidV7>] public readonly partial struct PaymentId;

// SQL Server-ordered sequential Guid.
[OrionId<Guid, SequentialGuid>] public readonly partial struct InvoiceId;

// Snowflake: 64-bit time-sortable long, embeds a worker id.
[OrionId<long, Snowflake>] public readonly partial struct UserId;

// ULID: sortable 26-char Crockford base32 string.
[OrionId<string, Ulid>] public readonly partial struct TenantId;

// KSUID: sortable 27-char string.
[OrionId<string, Ksuid>] public readonly partial struct EventId;

// MongoDB ObjectId: 24-char hex, sortable.
[OrionId<string, ObjectId>] public readonly partial struct DocumentId;

// NanoId: compact random string (not sortable).
[OrionId<string, NanoId>] public readonly partial struct SessionId;

// CUID2: collision-resistant random string (not sortable).
[OrionId<string, Cuid2>] public readonly partial struct AccountId;

// Integer identity assigned by a database -- no New() factory by design.
[OrionId<long>] public readonly partial struct LedgerRowId;
