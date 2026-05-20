# OrionKey id-generation benchmarks

Measured with BenchmarkDotNet's default job (the `--job short` switch was passed but BenchmarkDotNet
0.14.0 ran the `DefaultJob` regardless; the full run still completed in roughly 2.5 minutes and
produced the statistically stable numbers below). Reproduce with
`dotnet run -c Release --project bench/Moongazing.OrionKey.Benchmarks`.

## Environment

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.22621.4317/22H2/2022Update/SunValley2)
Intel Core i7-7820HQ CPU 2.90GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
```

## Results

| Method    | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------- |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| RawGuid   |  82.55 ns | 1.652 ns | 1.464 ns |  1.00 |    0.02 |      - |         - |          NA |
| Snowflake | 241.02 ns | 0.093 ns | 0.087 ns |  2.92 |    0.05 |      - |         - |          NA |
| Ulid      |  99.63 ns | 2.039 ns | 3.233 ns |  1.21 |    0.04 | 0.0191 |      80 B |          NA |
| NanoId    | 153.30 ns | 3.089 ns | 5.725 ns |  1.86 |    0.08 | 0.0153 |      64 B |          NA |
| GuidV7    | 118.11 ns | 2.372 ns | 3.964 ns |  1.43 |    0.05 |      - |         - |          NA |
