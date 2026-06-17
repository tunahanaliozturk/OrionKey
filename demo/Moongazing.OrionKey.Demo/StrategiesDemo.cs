namespace Moongazing.OrionKey.Demo;

/// <summary>
/// Mints one fresh id from every generation strategy so you can eyeball the wire
/// format each one produces.
/// </summary>
internal static class StrategiesDemo
{
    public static void Run()
    {
        DemoConsole.Section("1. ID generation strategies");

        DemoConsole.Item("OrderId    (Guid)", OrderId.New());
        DemoConsole.Item("PaymentId  (GuidV7)", PaymentId.New());
        DemoConsole.Item("InvoiceId  (SequentialGuid)", InvoiceId.New());
        DemoConsole.Item("UserId     (Snowflake/long)", UserId.New());
        DemoConsole.Item("TenantId   (ULID)", TenantId.New());
        DemoConsole.Item("EventId    (KSUID)", EventId.New());
        DemoConsole.Item("DocumentId (ObjectId)", DocumentId.New());
        DemoConsole.Item("SessionId  (NanoId)", SessionId.New());
        DemoConsole.Item("AccountId  (CUID2)", AccountId.New());

        DemoConsole.Note("LedgerRowId is [OrionId<long>] with no strategy: no New() factory.");
        DemoConsole.Note("It models a DB identity column, constructed from an assigned value:");
        var assigned = new LedgerRowId(42);
        DemoConsole.Item("LedgerRowId (assigned)", assigned);
    }
}
