using System.Text.Json.Serialization;

namespace Moongazing.OrionKey.AotSample;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(OrderId))]
[JsonSerializable(typeof(AuditId))]
[JsonSerializable(typeof(InvoiceId))]
[JsonSerializable(typeof(UserId))]
[JsonSerializable(typeof(TenantId))]
[JsonSerializable(typeof(SessionId))]
[JsonSerializable(typeof(AccountId))]
[JsonSerializable(typeof(EventId))]
[JsonSerializable(typeof(DocumentId))]
[JsonSerializable(typeof(TraceId))]
internal sealed partial class SampleJsonContext : JsonSerializerContext;
