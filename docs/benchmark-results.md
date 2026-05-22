# OrionKey id-generation benchmarks

Measured with BenchmarkDotNet's default job (the `--job short` switch was passed but BenchmarkDotNet
0.14.0 ran the `DefaultJob` regardless; the full run still completed in roughly 4 minutes and
produced the statistically stable numbers below). Reproduce with
`dotnet run -c Release --project bench/Moongazing.OrionKey.Benchmarks`.

## Environment

```text
BenchmarkDotNet v0.14.0, Windows 11 (10.0.22621.4317/22H2/2022Update/SunValley2)
Intel Core i7-7820HQ CPU 2.90GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
```

## Results

| Method         | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------- |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| RawGuid        |    70.19 ns |  0.611 ns |  0.510 ns |  1.00 |    0.01 |      - |         - |          NA |
| Snowflake      |   241.43 ns |  0.073 ns |  0.068 ns |  3.44 |    0.02 |      - |         - |          NA |
| Ulid           |   101.56 ns |  2.107 ns |  4.398 ns |  1.45 |    0.06 | 0.0191 |      80 B |          NA |
| NanoId         |   147.43 ns |  2.919 ns |  3.475 ns |  2.10 |    0.05 | 0.0153 |      64 B |          NA |
| GuidV7         |   121.92 ns |  2.315 ns |  2.377 ns |  1.74 |    0.04 |      - |         - |          NA |
| Cuid2          | 3,480.20 ns | 69.405 ns | 99.539 ns | 49.59 |    1.44 | 0.0153 |      72 B |          NA |
| Ksuid          | 1,510.77 ns | 29.633 ns | 45.253 ns | 21.53 |    0.65 | 0.2270 |     952 B |          NA |
| ObjectId       |    87.82 ns |  1.449 ns |  1.355 ns |  1.25 |    0.02 | 0.0343 |     144 B |          NA |
| SequentialGuid |   144.65 ns |  2.901 ns |  5.305 ns |  2.06 |    0.08 |      - |         - |          NA |
