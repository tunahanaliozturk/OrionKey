using Moongazing.OrionKey;

namespace Moongazing.OrionKey.Benchmarks;

// Strongly-typed ids used across the benchmark suite. Declared here (not pulled from the
// sample project) so the suite is self-contained and exercises one id per backing kind:
//
//   OrderId  - Guid-backed, default strategy (Guid.NewGuid under the hood).
//   UserId   - long-backed, Snowflake strategy (zero-allocation, time-sortable).
//   TenantId - string-backed, ULID strategy (26-char canonical, sortable).
//
// The source generator emits New(), Parse/TryParse (string + ReadOnlySpan<char>), ToString,
// ISpanFormattable/IUtf8SpanFormattable TryFormat, Equals/GetHashCode, and IComparable for each.
[OrionId<System.Guid>] public readonly partial struct OrderId;
[OrionId<long, Snowflake>] public readonly partial struct UserId;
[OrionId<string, Ulid>] public readonly partial struct TenantId;
