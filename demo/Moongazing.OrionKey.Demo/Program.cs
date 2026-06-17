using Moongazing.OrionKey;
using Moongazing.OrionKey.Demo;

// Snowflake ids embed a per-process worker id (10 bits, 0..1023). Pin it once at startup
// so every replica mints distinct ids. Without this OrionKey derives one from the machine
// name and logs a one-time warning.
OrionKey.Configure(o => o.SnowflakeWorkerId = 1);

Console.WriteLine();
Console.WriteLine("###################################################################");
Console.WriteLine("#                                                                 #");
Console.WriteLine("#   OrionKey Demo - source-generated strongly-typed IDs for .NET  #");
Console.WriteLine("#                                                                 #");
Console.WriteLine("###################################################################");

StrategiesDemo.Run();
EqualityDemo.Run();
OrderingDemo.Run();
ParsingDemo.Run();
FormattingDemo.Run();
JsonDemo.Run();

Console.WriteLine();
Console.WriteLine("All demos completed.");
