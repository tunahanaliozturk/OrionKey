# OrionKey v0.1.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build OrionKey — a standalone .NET library whose single `[OrionId<TValue, TStrategy>]` attribute makes a `readonly partial struct` a fully-featured strongly-typed ID (equality, comparison, `New()`, EF Core / JSON / TypeConverter / IParsable companions), all emitted by a bundled Roslyn incremental source generator.

**Architecture:** A single NuGet package `OrionKey` ships a multi-target (net8/9/10) runtime assembly plus a netstandard2.0 Roslyn generator packed as an analyzer. The runtime owns the ID algorithms (Snowflake, ULID, NanoId, GuidV7) and the `[OrionId]` attribute; the generator detects decorated structs and emits the struct body and converter companions. A second package `OrionKey.Testing` provides deterministic ID generators for tests.

**Tech Stack:** .NET 8/9/10, C# latest, Roslyn incremental generators (`Microsoft.CodeAnalysis.CSharp` 4.8.0), xUnit, `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing` for generator tests, EF Core (SQLite) + ASP.NET Core for integration tests, BenchmarkDotNet.

**Spec:** `docs/superpowers/specs/2026-05-20-orionkey-design.md`

**Repository:** `Desktop/OrionKey/`, fresh git repo, branch `main`. The design spec is already committed (`19e0d6e`).

---

## Conventions (apply to every task)

- **No `Co-Authored-By` trailer** in commit messages. **No emojis** anywhere.
- Test framework: xUnit, `[Fact]`/`[Theory]`, plain `Assert.X`. Naming `MethodUnderTest_ShouldDoX_WhenY`.
- All projects multi-target `net8.0;net9.0;net10.0` via `Directory.Build.props`, except the generator (netstandard2.0) and test/bench/sample projects (`IsPackable=false` → single `net10.0`).
- `TreatWarningsAsErrors=true` is on. Code must be warning-clean.
- Commit after every task with the message given in the task's final step.
- Verification: `dotnet build` clean and `dotnet test` green before each commit.
- Runtime namespace: `Moongazing.OrionKey`. Generator namespace: `Moongazing.OrionKey.Generators`.

---

## File Structure

### `src/Moongazing.OrionKey` → package `OrionKey` (runtime, net8/9/10)

| Path | Responsibility |
|---|---|
| `OrionIdAttribute.cs` | `OrionIdAttribute<TValue>` and `OrionIdAttribute<TValue,TStrategy>` |
| `Strategies.cs` | marker structs `Snowflake`, `Ulid`, `NanoId`, `GuidV7` |
| `OrionKeyOptions.cs` | `SnowflakeWorkerId`, `SnowflakeEpoch`, `EnableMetrics` |
| `OrionKey.cs` | static facade: `Configure`, internal `NextSnowflake`/`NewUlid`/`NewNanoId`/`NewGuidV7` |
| `Snowflake/SnowflakeIdGenerator.cs` | the Snowflake algorithm |
| `Snowflake/WorkerIdResolver.cs` | env-var / machine-name worker-id derivation |
| `Ulid/UlidFactory.cs` | ULID generation + Crockford base32 |
| `NanoId/NanoIdFactory.cs` | NanoId generation |
| `GuidV7/GuidV7Factory.cs` | UUIDv7 (native net9+, polyfill net8) |
| `OrionKeyClockException.cs` | thrown on Snowflake clock regression |
| `Diagnostics/OrionKeyDiagnostics.cs` | one-time warning channel + opt-in `Meter` |

### `src/Moongazing.OrionKey.Generators` → Roslyn generator (netstandard2.0, packed into `OrionKey`)

| Path | Responsibility |
|---|---|
| `OrionIdGenerator.cs` | the `IIncrementalGenerator` entry point |
| `Model/OrionIdModel.cs` | parsed model of one decorated struct |
| `Model/ValueType.cs` + `StrategyType.cs` | enums for TValue / TStrategy |
| `Parsing/OrionIdParser.cs` | symbol → model, with diagnostics |
| `Diagnostics/OrionKeyDiagnostics.cs` | `DiagnosticDescriptor` constants ORIONKEY001-005 |
| `Emit/CoreBodyEmitter.cs` | struct body + `New()` |
| `Emit/ComparableEmitter.cs` | `IComparable` + comparison operators |
| `Emit/JsonConverterEmitter.cs` | `System.Text.Json` converter |
| `Emit/TypeConverterEmitter.cs` | `TypeConverter` |
| `Emit/ParsableEmitter.cs` | `IParsable`/`ISpanParsable` |
| `Emit/EfCoreConverterEmitter.cs` | EF Core `ValueConverter` (conditional) |

### `src/Moongazing.OrionKey.Testing` → package `OrionKey.Testing`

| Path | Responsibility |
|---|---|
| `DeterministicIdScope.cs` | `IDisposable` that swaps + restores process generators |
| `SequentialGenerators.cs` | `SequentialSnowflake`, `SequentialUlid`, `SequentialNanoId` |

### tests / bench / sample

`tests/Moongazing.OrionKey.Tests`, `tests/Moongazing.OrionKey.Generators.Tests`, `tests/Moongazing.OrionKey.IntegrationTests`, `tests/Moongazing.OrionKey.Testing.Tests`, `bench/Moongazing.OrionKey.Benchmarks`, `sample/Moongazing.OrionKey.Sample`.

---

## Task 1: Scaffold the solution and projects

**Files:** the whole skeleton.

- [ ] **Step 1: Create `Directory.Build.props` at repo root**

```xml
<Project>
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>

  <PropertyGroup Condition="'$(IsPackable)' != 'false'">
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>CS1591;NU1900;NU1901;NU1902;NU1903;NU1904</NoWarn>
    <Authors>Tunahan Ali Ozturk</Authors>
    <Company>Tunahan Ali Ozturk</Company>
    <RepositoryUrl>https://github.com/tunahanaliozturk/OrionKey</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageProjectUrl>https://github.com/tunahanaliozturk/OrionKey</PackageProjectUrl>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <Version>0.1.0</Version>
  </PropertyGroup>

  <PropertyGroup Condition="'$(IsPackable)' == 'false'">
    <TargetFramework>net10.0</TargetFramework>
    <TargetFrameworks></TargetFrameworks>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create the runtime project `src/Moongazing.OrionKey/Moongazing.OrionKey.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>OrionKey</PackageId>
    <Description>Source-generated strongly-typed IDs for .NET. One attribute emits equality, comparison, New() factory, EF Core ValueConverter, System.Text.Json converter, TypeConverter, and IParsable/ISpanParsable. Strategies: Guid, GuidV7, Snowflake, ULID, NanoId.</Description>
    <PackageTags>strongly-typed-id;source-generator;ddd;guid;snowflake;ulid;nanoid;ef-core</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Moongazing.OrionKey.Generators\Moongazing.OrionKey.Generators.csproj"
                      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>
  <ItemGroup>
    <None Include="$(OutputPath)\..\..\Moongazing.OrionKey.Generators\$(Configuration)\netstandard2.0\Moongazing.OrionKey.Generators.dll"
          Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
  </ItemGroup>
</Project>
```

Note: the `None Include` packs the generator DLL into the `analyzers/dotnet/cs` folder of the `OrionKey` package. If the relative path resolution proves fragile, an alternative is a `TargetsForTfmSpecificContentInPackage` hook — but try the simple `None Include` first and verify in Task 17.

- [ ] **Step 3: Create the generator project `src/Moongazing.OrionKey.Generators/Moongazing.OrionKey.Generators.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <TargetFrameworks></TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <IsRoslynComponent>true</IsRoslynComponent>
    <IsPackable>false</IsPackable>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create the testing project `src/Moongazing.OrionKey.Testing/Moongazing.OrionKey.Testing.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>OrionKey.Testing</PackageId>
    <Description>Deterministic ID generators for testing code that uses OrionKey strongly-typed IDs.</Description>
    <PackageTags>strongly-typed-id;testing;orionkey</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Moongazing.OrionKey\Moongazing.OrionKey.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Create the four test projects, the bench project, and the sample project**

Each test project csproj (`tests/Moongazing.OrionKey.Tests`, `.Generators.Tests`, `.IntegrationTests`, `.Testing.Tests`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <!-- ProjectReferences added per-project below -->
</Project>
```

ProjectReferences:
- `.Tests` → `Moongazing.OrionKey`
- `.Generators.Tests` → `Moongazing.OrionKey.Generators` and `Moongazing.OrionKey`; plus `<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />`
- `.IntegrationTests` → `Moongazing.OrionKey`; plus EF Core SQLite and ASP.NET Core (added in Task 19)
- `.Testing.Tests` → `Moongazing.OrionKey.Testing`

`bench/Moongazing.OrionKey.Benchmarks` (`IsPackable=false`, references `Moongazing.OrionKey`, `<PackageReference Include="BenchmarkDotNet" Version="0.14.0" />`, an `<OutputType>Exe</OutputType>`).

`sample/Moongazing.OrionKey.Sample` (`IsPackable=false`, `<OutputType>Exe</OutputType>`, references `Moongazing.OrionKey`).

- [ ] **Step 6: Create the solution and add every project**

```
dotnet new sln -n Moongazing.OrionKey
dotnet sln add src/Moongazing.OrionKey src/Moongazing.OrionKey.Generators src/Moongazing.OrionKey.Testing
dotnet sln add tests/Moongazing.OrionKey.Tests tests/Moongazing.OrionKey.Generators.Tests tests/Moongazing.OrionKey.IntegrationTests tests/Moongazing.OrionKey.Testing.Tests
dotnet sln add bench/Moongazing.OrionKey.Benchmarks sample/Moongazing.OrionKey.Sample
```

- [ ] **Step 7: Add a placeholder so each project compiles**

Each `src` project needs at least one `.cs` file to build. Add a temporary `namespace Moongazing.OrionKey;` one-liner file `_Placeholder.cs` in each src project (deleted as real files arrive). Each test project needs one trivial passing test, e.g. `tests/Moongazing.OrionKey.Tests/SmokeTest.cs`:

```csharp
namespace Moongazing.OrionKey.Tests;

public class SmokeTest
{
    [Fact]
    public void Solution_Builds() => Assert.True(true);
}
```

- [ ] **Step 8: Build the whole solution**

Run: `dotnet build`
Expected: success, 0 warnings (TreatWarningsAsErrors is on).

- [ ] **Step 9: Commit**

```
git add -A
git commit -m "chore(orionkey): scaffold solution, projects, Directory.Build.props"
```

---

## Task 2: `OrionKeyOptions`, `WorkerIdResolver`, `OrionKey.Configure`

**Files:**
- Create: `src/Moongazing.OrionKey/OrionKeyOptions.cs`
- Create: `src/Moongazing.OrionKey/Snowflake/WorkerIdResolver.cs`
- Create: `src/Moongazing.OrionKey/Diagnostics/OrionKeyDiagnostics.cs`
- Test: `tests/Moongazing.OrionKey.Tests/WorkerIdResolverTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/Moongazing.OrionKey.Tests/WorkerIdResolverTests.cs`:

```csharp
using Moongazing.OrionKey.Snowflake;

namespace Moongazing.OrionKey.Tests;

public class WorkerIdResolverTests
{
    [Fact]
    public void Resolve_ShouldUseEnvironmentVariable_WhenSet()
    {
        Environment.SetEnvironmentVariable("ORIONKEY_WORKER_ID", "42");
        try
        {
            var (workerId, source) = WorkerIdResolver.Resolve();
            Assert.Equal(42, workerId);
            Assert.Equal(WorkerIdSource.EnvironmentVariable, source);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ORIONKEY_WORKER_ID", null);
        }
    }

    [Fact]
    public void Resolve_ShouldFallBackToMachineHash_WhenEnvVarMissing()
    {
        Environment.SetEnvironmentVariable("ORIONKEY_WORKER_ID", null);
        var (workerId, source) = WorkerIdResolver.Resolve();
        Assert.InRange(workerId, 0, 1023);
        Assert.Equal(WorkerIdSource.MachineNameHash, source);
    }

    [Fact]
    public void Resolve_ShouldMaskMachineHashTo10Bits()
    {
        var (workerId, _) = WorkerIdResolver.Resolve();
        Assert.True(workerId <= 1023);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1024")]
    [InlineData("notanumber")]
    public void Resolve_ShouldFallBackToMachineHash_WhenEnvVarInvalid(string raw)
    {
        Environment.SetEnvironmentVariable("ORIONKEY_WORKER_ID", raw);
        try
        {
            var (_, source) = WorkerIdResolver.Resolve();
            Assert.Equal(WorkerIdSource.MachineNameHash, source);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ORIONKEY_WORKER_ID", null);
        }
    }
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Tests --filter WorkerIdResolverTests`
Expected: build error — `WorkerIdResolver` does not exist.

- [ ] **Step 3: Create `WorkerIdResolver.cs`**

```csharp
namespace Moongazing.OrionKey.Snowflake;

/// <summary>How a Snowflake worker id was determined.</summary>
public enum WorkerIdSource
{
    /// <summary>Set explicitly via <see cref="OrionKeyOptions.SnowflakeWorkerId"/>.</summary>
    Explicit,
    /// <summary>Read from the <c>ORIONKEY_WORKER_ID</c> environment variable.</summary>
    EnvironmentVariable,
    /// <summary>Derived from a hash of the machine name (fallback).</summary>
    MachineNameHash,
}

/// <summary>Resolves a 10-bit Snowflake worker id when none is configured explicitly.</summary>
public static class WorkerIdResolver
{
    /// <summary>Maximum worker id value (10 bits).</summary>
    public const int MaxWorkerId = 1023;

    private const string EnvVarName = "ORIONKEY_WORKER_ID";

    /// <summary>Resolves a worker id from the environment variable, else a machine-name hash.</summary>
    public static (int WorkerId, WorkerIdSource Source) Resolve()
    {
        var raw = Environment.GetEnvironmentVariable(EnvVarName);
        if (int.TryParse(raw, out var parsed) && parsed is >= 0 and <= MaxWorkerId)
        {
            return (parsed, WorkerIdSource.EnvironmentVariable);
        }

        var hash = MachineNameHash(Environment.MachineName);
        return (hash & MaxWorkerId, WorkerIdSource.MachineNameHash);
    }

    private static int MachineNameHash(string machineName)
    {
        // FNV-1a 32-bit — stable across processes and runtimes (string.GetHashCode is randomized).
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var c in machineName)
        {
            hash = (hash ^ c) * prime;
        }
        return (int)(hash & int.MaxValue);
    }
}
```

- [ ] **Step 4: Create `OrionKeyOptions.cs`**

```csharp
namespace Moongazing.OrionKey;

/// <summary>Process-wide OrionKey configuration. Set once via <see cref="OrionKey.Configure"/>.</summary>
public sealed class OrionKeyOptions
{
    /// <summary>
    /// Snowflake worker id (0..1023). When null, OrionKey derives one from the
    /// <c>ORIONKEY_WORKER_ID</c> environment variable or a machine-name hash.
    /// </summary>
    public int? SnowflakeWorkerId { get; set; }

    /// <summary>Snowflake epoch. Defaults to 2025-01-01 UTC.</summary>
    public DateTime SnowflakeEpoch { get; set; } = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>When true, the <c>orionkey.ids.generated</c> meter counter is recorded. Default false.</summary>
    public bool EnableMetrics { get; set; }
}
```

- [ ] **Step 5: Create `Diagnostics/OrionKeyDiagnostics.cs`**

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Moongazing.OrionKey.Diagnostics;

/// <summary>OrionKey runtime diagnostics: a one-time worker-id warning and an opt-in counter.</summary>
public static class OrionKeyDiagnostics
{
    /// <summary>The OrionKey meter name.</summary>
    public const string MeterName = "Moongazing.OrionKey";

    private static readonly Meter Meter = new(MeterName, "0.1.0");
    private static readonly Counter<long> IdsGenerated =
        Meter.CreateCounter<long>("orionkey.ids.generated");

    private static int workerIdWarningWritten;

    /// <summary>Records one generated id against the meter (no-op unless metrics are enabled).</summary>
    public static void RecordGenerated(string strategy, bool metricsEnabled)
    {
        if (metricsEnabled)
        {
            IdsGenerated.Add(1, new KeyValuePair<string, object?>("strategy", strategy));
        }
    }

    /// <summary>Writes the auto-derived-worker-id warning to the trace listeners exactly once.</summary>
    public static void WarnAutoWorkerIdOnce(int workerId)
    {
        if (Interlocked.Exchange(ref workerIdWarningWritten, 1) == 0)
        {
            Trace.TraceWarning(
                $"OrionKey: Snowflake worker id {workerId} was auto-derived from the machine name. " +
                "In a multi-instance deployment, set ORIONKEY_WORKER_ID or call OrionKey.Configure " +
                "to assign a unique id per instance and avoid id collisions.");
        }
    }
}
```

- [ ] **Step 6: Run tests, expect PASS**

Run: `dotnet test tests/Moongazing.OrionKey.Tests --filter WorkerIdResolverTests`
Expected: 6 tests pass.

- [ ] **Step 7: Commit**

```
git add src/Moongazing.OrionKey tests/Moongazing.OrionKey.Tests/WorkerIdResolverTests.cs
git commit -m "feat(orionkey): OrionKeyOptions, WorkerIdResolver, runtime diagnostics"
```

---

## Task 3: `SnowflakeIdGenerator`

**Files:**
- Create: `src/Moongazing.OrionKey/OrionKeyClockException.cs`
- Create: `src/Moongazing.OrionKey/Snowflake/SnowflakeIdGenerator.cs`
- Test: `tests/Moongazing.OrionKey.Tests/SnowflakeIdGeneratorTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Moongazing.OrionKey.Snowflake;

namespace Moongazing.OrionKey.Tests;

public class SnowflakeIdGeneratorTests
{
    private static readonly DateTime Epoch = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Next_ShouldProduceStrictlyIncreasingIds()
    {
        var gen = new SnowflakeIdGenerator(workerId: 1, Epoch);
        long previous = 0;
        for (var i = 0; i < 10_000; i++)
        {
            var id = gen.Next();
            Assert.True(id > previous, $"id {id} not greater than {previous}");
            previous = id;
        }
    }

    [Fact]
    public void Next_ShouldProduceUniqueIds_UnderParallelLoad()
    {
        var gen = new SnowflakeIdGenerator(workerId: 7, Epoch);
        var ids = new System.Collections.Concurrent.ConcurrentBag<long>();
        Parallel.For(0, 50_000, _ => ids.Add(gen.Next()));
        Assert.Equal(50_000, ids.Distinct().Count());
    }

    [Fact]
    public void Next_ShouldEncodeWorkerIdInBits12To21()
    {
        var gen = new SnowflakeIdGenerator(workerId: 513, Epoch);
        var id = gen.Next();
        var worker = (id >> 12) & 0x3FF;
        Assert.Equal(513, worker);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1024)]
    public void Ctor_ShouldThrow_WhenWorkerIdOutOfRange(int workerId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnowflakeIdGenerator(workerId, Epoch));
    }

    [Fact]
    public void Next_ShouldThrowClockException_WhenClockMovesBackwards()
    {
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        var gen = new SnowflakeIdGenerator(workerId: 1, Epoch, clock.Now);
        gen.Next();
        clock.Rewind(TimeSpan.FromSeconds(5));
        Assert.Throws<OrionKeyClockException>(() => gen.Next());
    }

    private sealed class MutableClock(DateTimeOffset start)
    {
        private long ticks = start.UtcTicks;
        public DateTimeOffset Now() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
        public void Rewind(TimeSpan by) => Interlocked.Add(ref ticks, -by.Ticks);
    }
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Tests --filter SnowflakeIdGeneratorTests`
Expected: build error.

- [ ] **Step 3: Create `OrionKeyClockException.cs`**

```csharp
namespace Moongazing.OrionKey;

/// <summary>Thrown when the system clock moves backwards beyond the Snowflake tolerance.</summary>
public sealed class OrionKeyClockException : Exception
{
    /// <summary>Initializes the exception with a message.</summary>
    public OrionKeyClockException(string message) : base(message) { }
}
```

- [ ] **Step 4: Create `SnowflakeIdGenerator.cs`**

```csharp
namespace Moongazing.OrionKey.Snowflake;

/// <summary>
/// Twitter-Snowflake id generator: <c>41-bit ms-timestamp | 10-bit worker | 12-bit sequence</c>.
/// Thread-safe. Ids are strictly increasing per process.
/// </summary>
public sealed class SnowflakeIdGenerator
{
    private const int WorkerBits = 10;
    private const int SequenceBits = 12;
    private const int MaxSequence = (1 << SequenceBits) - 1;   // 4095
    private const int WorkerShift = SequenceBits;
    private const int TimestampShift = SequenceBits + WorkerBits;

    private readonly long workerId;
    private readonly long epochMs;
    private readonly Func<DateTimeOffset> clock;
    private readonly object gate = new();

    private long lastTimestamp = -1;
    private long sequence;

    /// <summary>Creates a generator using the system clock.</summary>
    public SnowflakeIdGenerator(int workerId, DateTime epoch)
        : this(workerId, epoch, static () => DateTimeOffset.UtcNow) { }

    /// <summary>Creates a generator with an injectable clock (for tests).</summary>
    public SnowflakeIdGenerator(int workerId, DateTime epoch, Func<DateTimeOffset> clock)
    {
        if (workerId is < 0 or > WorkerIdResolver.MaxWorkerId)
        {
            throw new ArgumentOutOfRangeException(nameof(workerId), workerId,
                $"Worker id must be between 0 and {WorkerIdResolver.MaxWorkerId}.");
        }
        this.workerId = workerId;
        this.clock = clock;
        epochMs = new DateTimeOffset(epoch.ToUniversalTime()).ToUnixTimeMilliseconds();
    }

    /// <summary>Generates the next id.</summary>
    /// <exception cref="OrionKeyClockException">The clock moved backwards.</exception>
    public long Next()
    {
        lock (gate)
        {
            var timestamp = clock().ToUnixTimeMilliseconds();

            if (timestamp < lastTimestamp)
            {
                throw new OrionKeyClockException(
                    $"Clock moved backwards by {lastTimestamp - timestamp} ms. Refusing to generate an id.");
            }

            if (timestamp == lastTimestamp)
            {
                sequence = (sequence + 1) & MaxSequence;
                if (sequence == 0)
                {
                    // Sequence exhausted this millisecond — spin to the next.
                    timestamp = WaitNextMillis(lastTimestamp);
                }
            }
            else
            {
                sequence = 0;
            }

            lastTimestamp = timestamp;

            return ((timestamp - epochMs) << TimestampShift)
                 | (workerId << WorkerShift)
                 | sequence;
        }
    }

    private long WaitNextMillis(long lastTs)
    {
        long ts;
        do
        {
            ts = clock().ToUnixTimeMilliseconds();
        }
        while (ts <= lastTs);
        return ts;
    }
}
```

- [ ] **Step 5: Run tests, expect PASS**

Run: `dotnet test tests/Moongazing.OrionKey.Tests --filter SnowflakeIdGeneratorTests`
Expected: 6 tests pass.

- [ ] **Step 6: Commit**

```
git add src/Moongazing.OrionKey tests/Moongazing.OrionKey.Tests/SnowflakeIdGeneratorTests.cs
git commit -m "feat(orionkey): thread-safe SnowflakeIdGenerator"
```

---

## Task 4: `UlidFactory`

**Files:**
- Create: `src/Moongazing.OrionKey/Ulid/UlidFactory.cs`
- Test: `tests/Moongazing.OrionKey.Tests/UlidFactoryTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Moongazing.OrionKey.Ulid;

namespace Moongazing.OrionKey.Tests;

public class UlidFactoryTests
{
    [Fact]
    public void NewUlid_ShouldBe26Characters()
    {
        Assert.Equal(26, UlidFactory.NewUlid().Length);
    }

    [Fact]
    public void NewUlid_ShouldUseOnlyCrockfordBase32Alphabet()
    {
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        var ulid = UlidFactory.NewUlid();
        Assert.All(ulid, c => Assert.Contains(c, alphabet));
    }

    [Fact]
    public void NewUlid_ShouldSortLexicographicallyByCreationOrder()
    {
        var first = UlidFactory.NewUlid();
        Thread.Sleep(2);
        var second = UlidFactory.NewUlid();
        Assert.True(string.CompareOrdinal(first, second) < 0);
    }

    [Fact]
    public void NewUlid_ShouldBeMonotonic_WithinSameMillisecond()
    {
        var ulids = new string[1000];
        for (var i = 0; i < ulids.Length; i++)
        {
            ulids[i] = UlidFactory.NewUlid();
        }
        for (var i = 1; i < ulids.Length; i++)
        {
            Assert.True(string.CompareOrdinal(ulids[i - 1], ulids[i]) < 0,
                $"ULID at {i} not strictly greater than predecessor");
        }
    }

    [Fact]
    public void NewUlid_ShouldBeUnique_AcrossManyCalls()
    {
        var set = new HashSet<string>();
        for (var i = 0; i < 50_000; i++)
        {
            Assert.True(set.Add(UlidFactory.NewUlid()));
        }
    }
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Tests --filter UlidFactoryTests`
Expected: build error.

- [ ] **Step 3: Create `UlidFactory.cs`**

```csharp
using System.Security.Cryptography;

namespace Moongazing.OrionKey.Ulid;

/// <summary>
/// Generates ULIDs: a 48-bit millisecond timestamp followed by 80 bits of randomness,
/// encoded as 26 Crockford base32 characters. Monotonic within a millisecond.
/// </summary>
public static class UlidFactory
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private static readonly object Gate = new();
    private static long lastTimestamp = -1;
    private static readonly byte[] LastRandomness = new byte[10];

    /// <summary>Generates a new 26-character ULID string.</summary>
    public static string NewUlid()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Span<byte> randomness = stackalloc byte[10];

        lock (Gate)
        {
            if (timestamp == lastTimestamp)
            {
                // Same millisecond: increment the previous randomness for monotonicity.
                IncrementBigEndian(LastRandomness);
            }
            else
            {
                RandomNumberGenerator.Fill(LastRandomness);
                lastTimestamp = timestamp;
            }
            LastRandomness.CopyTo(randomness);
        }

        return Encode(timestamp, randomness);
    }

    private static void IncrementBigEndian(byte[] bytes)
    {
        for (var i = bytes.Length - 1; i >= 0; i--)
        {
            if (++bytes[i] != 0)
            {
                return;
            }
        }
    }

    private static string Encode(long timestamp, ReadOnlySpan<byte> randomness)
    {
        // ULID layout: 10 chars timestamp (48 bits) + 16 chars randomness (80 bits).
        Span<char> chars = stackalloc char[26];

        for (var i = 9; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(timestamp & 0x1F)];
            timestamp >>= 5;
        }

        // 80 random bits -> 16 base32 chars. Treat the 10 bytes as a big-endian bit stream.
        var bitBuffer = 0;
        var bitCount = 0;
        var outIndex = 10;
        foreach (var b in randomness)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitCount += 8;
            while (bitCount >= 5)
            {
                bitCount -= 5;
                chars[outIndex++] = Alphabet[(bitBuffer >> bitCount) & 0x1F];
            }
        }

        return new string(chars);
    }
}
```

- [ ] **Step 4: Run tests, expect PASS**

Run: `dotnet test tests/Moongazing.OrionKey.Tests --filter UlidFactoryTests`
Expected: 5 tests pass.

- [ ] **Step 5: Commit**

```
git add src/Moongazing.OrionKey tests/Moongazing.OrionKey.Tests/UlidFactoryTests.cs
git commit -m "feat(orionkey): monotonic UlidFactory with Crockford base32"
```

---

## Task 5: `NanoIdFactory`

**Files:**
- Create: `src/Moongazing.OrionKey/NanoId/NanoIdFactory.cs`
- Test: `tests/Moongazing.OrionKey.Tests/NanoIdFactoryTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Moongazing.OrionKey.NanoId;

namespace Moongazing.OrionKey.Tests;

public class NanoIdFactoryTests
{
    [Fact]
    public void NewNanoId_ShouldBe21Characters()
    {
        Assert.Equal(21, NanoIdFactory.NewNanoId().Length);
    }

    [Fact]
    public void NewNanoId_ShouldUseOnlyUrlSafeAlphabet()
    {
        const string alphabet =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-";
        var id = NanoIdFactory.NewNanoId();
        Assert.All(id, c => Assert.Contains(c, alphabet));
    }

    [Fact]
    public void NewNanoId_ShouldBeUnique_AcrossManyCalls()
    {
        var set = new HashSet<string>();
        for (var i = 0; i < 100_000; i++)
        {
            Assert.True(set.Add(NanoIdFactory.NewNanoId()));
        }
    }
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Tests --filter NanoIdFactoryTests`
Expected: build error.

- [ ] **Step 3: Create `NanoIdFactory.cs`**

```csharp
using System.Security.Cryptography;

namespace Moongazing.OrionKey.NanoId;

/// <summary>
/// Generates NanoIds: 21 characters drawn uniformly from a 64-character URL-safe alphabet,
/// sourced from a cryptographic random number generator.
/// </summary>
public static class NanoIdFactory
{
    private const string Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-";
    private const int Size = 21;

    /// <summary>Generates a new 21-character NanoId string.</summary>
    public static string NewNanoId()
    {
        // The alphabet is exactly 64 chars, so 6 bits per char map with no modulo bias.
        Span<byte> bytes = stackalloc byte[Size];
        RandomNumberGenerator.Fill(bytes);

        Span<char> chars = stackalloc char[Size];
        for (var i = 0; i < Size; i++)
        {
            chars[i] = Alphabet[bytes[i] & 63];
        }
        return new string(chars);
    }
}
```

- [ ] **Step 4: Run tests, expect PASS**

Run: `dotnet test tests/Moongazing.OrionKey.Tests --filter NanoIdFactoryTests`
Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```
git add src/Moongazing.OrionKey tests/Moongazing.OrionKey.Tests/NanoIdFactoryTests.cs
git commit -m "feat(orionkey): NanoIdFactory with URL-safe alphabet"
```

---

## Task 6: `GuidV7Factory`

**Files:**
- Create: `src/Moongazing.OrionKey/GuidV7/GuidV7Factory.cs`
- Test: `tests/Moongazing.OrionKey.Tests/GuidV7FactoryTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Moongazing.OrionKey.GuidV7;

namespace Moongazing.OrionKey.Tests;

public class GuidV7FactoryTests
{
    [Fact]
    public void NewGuidV7_ShouldSetVersionNibbleTo7()
    {
        var guid = GuidV7Factory.NewGuidV7();
        var bytes = guid.ToByteArray();
        // Version is the high nibble of byte 7 in big-endian RFC layout.
        // Guid.ToByteArray() is little-endian for the first 3 groups; byte index 7 holds version.
        var version = (bytes[7] & 0xF0) >> 4;
        Assert.Equal(7, version);
    }

    [Fact]
    public void NewGuidV7_ShouldSortByCreationOrder()
    {
        var first = GuidV7Factory.NewGuidV7();
        Thread.Sleep(2);
        var second = GuidV7Factory.NewGuidV7();
        Assert.True(CompareGuids(first, second) < 0);
    }

    [Fact]
    public void NewGuidV7_ShouldBeUnique_AcrossManyCalls()
    {
        var set = new HashSet<Guid>();
        for (var i = 0; i < 50_000; i++)
        {
            Assert.True(set.Add(GuidV7Factory.NewGuidV7()));
        }
    }

    private static int CompareGuids(Guid a, Guid b)
        => string.CompareOrdinal(a.ToString("N"), b.ToString("N"));
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Tests --filter GuidV7FactoryTests`
Expected: build error.

- [ ] **Step 3: Create `GuidV7Factory.cs`**

`Guid.CreateVersion7()` is native on net9.0+. On net8.0 it must be polyfilled. Use a multi-targeting `#if`:

```csharp
using System.Security.Cryptography;

namespace Moongazing.OrionKey.GuidV7;

/// <summary>
/// Generates version-7 UUIDs (RFC 9562): a 48-bit Unix-millisecond timestamp followed by
/// random bits, sortable by creation time. Uses the BCL implementation on net9.0+ and a
/// conformant polyfill on net8.0.
/// </summary>
public static class GuidV7Factory
{
    /// <summary>Generates a new version-7 GUID.</summary>
#if NET9_0_OR_GREATER
    public static Guid NewGuidV7() => Guid.CreateVersion7();
#else
    public static Guid NewGuidV7()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);

        var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // Bytes 0-5: big-endian 48-bit timestamp.
        bytes[0] = (byte)(unixMs >> 40);
        bytes[1] = (byte)(unixMs >> 32);
        bytes[2] = (byte)(unixMs >> 24);
        bytes[3] = (byte)(unixMs >> 16);
        bytes[4] = (byte)(unixMs >> 8);
        bytes[5] = (byte)unixMs;
        // Byte 6 high nibble = version 7.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70);
        // Byte 8 high two bits = variant 0b10.
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        // The above byte order is the RFC big-endian layout. Convert to the Guid ctor,
        // which on .NET reads the first three groups little-endian.
        return new Guid(
            (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3],
            (short)((bytes[4] << 8) | bytes[5]),
            (short)((bytes[6] << 8) | bytes[7]),
            bytes[8], bytes[9], bytes[10], bytes[11],
            bytes[12], bytes[13], bytes[14], bytes[15]);
    }
#endif
}
```

- [ ] **Step 4: Run tests, expect PASS on all three TFMs**

Run: `dotnet test tests/Moongazing.OrionKey.Tests --filter GuidV7FactoryTests`
Expected: 3 tests pass. (The runtime library multi-targets; the test project runs net10.0 but the library's net8.0 path is compiled — confirm `dotnet build` is clean for net8.0 too.)

- [ ] **Step 5: Commit**

```
git add src/Moongazing.OrionKey tests/Moongazing.OrionKey.Tests/GuidV7FactoryTests.cs
git commit -m "feat(orionkey): GuidV7Factory with net8 polyfill"
```

---

## Task 7: Attribute, strategy markers, and the `OrionKey` static facade

**Files:**
- Create: `src/Moongazing.OrionKey/OrionIdAttribute.cs`
- Create: `src/Moongazing.OrionKey/Strategies.cs`
- Create: `src/Moongazing.OrionKey/OrionKey.cs`
- Delete: `src/Moongazing.OrionKey/_Placeholder.cs`
- Test: `tests/Moongazing.OrionKey.Tests/OrionKeyFacadeTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
namespace Moongazing.OrionKey.Tests;

[Collection("OrionKeyFacade")]   // serialize: OrionKey.Configure is process-global
public class OrionKeyFacadeTests
{
    [Fact]
    public void NextSnowflake_ShouldWork_WithoutExplicitConfigure()
    {
        // Auto worker-id fallback path — no Configure call.
        var a = OrionKey.NextSnowflake();
        var b = OrionKey.NextSnowflake();
        Assert.True(b > a);
    }

    [Fact]
    public void NewUlid_ShouldReturn26Chars()
    {
        Assert.Equal(26, OrionKey.NewUlid().Length);
    }

    [Fact]
    public void NewNanoId_ShouldReturn21Chars()
    {
        Assert.Equal(21, OrionKey.NewNanoId().Length);
    }

    [Fact]
    public void NewGuidV7_ShouldReturnNonEmptyGuid()
    {
        Assert.NotEqual(Guid.Empty, OrionKey.NewGuidV7());
    }
}
```

Add `[CollectionDefinition("OrionKeyFacade", DisableParallelization = true)]` in a small file `tests/Moongazing.OrionKey.Tests/Collections.cs`:

```csharp
namespace Moongazing.OrionKey.Tests;

[CollectionDefinition("OrionKeyFacade", DisableParallelization = true)]
public sealed class OrionKeyFacadeCollection;
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Tests --filter OrionKeyFacadeTests`
Expected: build error — `OrionKey` static type / members do not exist.

- [ ] **Step 3: Create `OrionIdAttribute.cs`**

```csharp
namespace Moongazing.OrionKey;

/// <summary>
/// Marks a <c>readonly partial struct</c> as a strongly-typed id backed by <typeparamref name="TValue"/>.
/// For <see cref="System.Guid"/> the generation strategy is implied; for <see cref="int"/> and
/// <see cref="long"/> the id is treated as externally assigned (database identity).
/// </summary>
/// <typeparam name="TValue">Underlying primitive: <see cref="System.Guid"/>, <see cref="int"/>,
/// <see cref="long"/>, or <see cref="string"/>.</typeparam>
[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class OrionIdAttribute<TValue> : Attribute;

/// <summary>
/// Marks a <c>readonly partial struct</c> as a strongly-typed id backed by <typeparamref name="TValue"/>
/// and generated using <typeparamref name="TStrategy"/>.
/// </summary>
/// <typeparam name="TValue">Underlying primitive type.</typeparam>
/// <typeparam name="TStrategy">Generation strategy: <see cref="Snowflake"/>, <see cref="Ulid"/>,
/// <see cref="NanoId"/>, or <see cref="GuidV7"/>.</typeparam>
[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class OrionIdAttribute<TValue, TStrategy> : Attribute;
```

- [ ] **Step 4: Create `Strategies.cs`**

```csharp
namespace Moongazing.OrionKey;

/// <summary>Strategy marker: 64-bit Twitter-Snowflake ids (sortable). Pairs with <see cref="long"/>.</summary>
public readonly struct Snowflake;

/// <summary>Strategy marker: 26-character ULID strings (sortable). Pairs with <see cref="string"/>.</summary>
public readonly struct Ulid;

/// <summary>Strategy marker: 21-character NanoId strings. Pairs with <see cref="string"/>.</summary>
public readonly struct NanoId;

/// <summary>Strategy marker: version-7 GUIDs (sortable). Pairs with <see cref="System.Guid"/>.</summary>
public readonly struct GuidV7;
```

- [ ] **Step 5: Create `OrionKey.cs`**

```csharp
using Moongazing.OrionKey.Diagnostics;
using Moongazing.OrionKey.GuidV7;
using Moongazing.OrionKey.NanoId;
using Moongazing.OrionKey.Snowflake;
using Moongazing.OrionKey.Ulid;

namespace Moongazing.OrionKey;

/// <summary>
/// Process-wide OrionKey facade. Call <see cref="Configure"/> once at startup to set the Snowflake
/// worker id; otherwise it is derived from the environment. Generated id structs call the
/// <c>New*</c> members; application code rarely calls them directly.
/// </summary>
public static class OrionKey
{
    private static readonly object Gate = new();
    private static OrionKeyOptions options = new();
    private static SnowflakeIdGenerator? snowflake;
    private static bool configured;

    /// <summary>
    /// Sets process-wide OrionKey configuration. Must be called at most once, before the first id
    /// is generated.
    /// </summary>
    /// <exception cref="InvalidOperationException">Configuration was already applied.</exception>
    public static void Configure(Action<OrionKeyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        lock (Gate)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "OrionKey.Configure has already been called. Configuration is process-global " +
                    "and cannot change after the first id is generated.");
            }
            var fresh = new OrionKeyOptions();
            configure(fresh);
            options = fresh;
            configured = true;
            snowflake = null; // rebuilt lazily with the new options
        }
    }

    /// <summary>Generates the next Snowflake id.</summary>
    public static long NextSnowflake()
    {
        var generator = GetSnowflake();
        var id = generator.Next();
        OrionKeyDiagnostics.RecordGenerated("snowflake", options.EnableMetrics);
        return id;
    }

    /// <summary>Generates a new ULID string.</summary>
    public static string NewUlid()
    {
        var id = UlidFactory.NewUlid();
        OrionKeyDiagnostics.RecordGenerated("ulid", options.EnableMetrics);
        return id;
    }

    /// <summary>Generates a new NanoId string.</summary>
    public static string NewNanoId()
    {
        var id = NanoIdFactory.NewNanoId();
        OrionKeyDiagnostics.RecordGenerated("nanoid", options.EnableMetrics);
        return id;
    }

    /// <summary>Generates a new version-7 GUID.</summary>
    public static Guid NewGuidV7()
    {
        var id = GuidV7Factory.NewGuidV7();
        OrionKeyDiagnostics.RecordGenerated("guidv7", options.EnableMetrics);
        return id;
    }

    private static SnowflakeIdGenerator GetSnowflake()
    {
        if (snowflake is not null)
        {
            return snowflake;
        }
        lock (Gate)
        {
            if (snowflake is null)
            {
                int workerId;
                if (options.SnowflakeWorkerId is { } explicitId)
                {
                    workerId = explicitId;
                }
                else
                {
                    var (resolved, source) = WorkerIdResolver.Resolve();
                    workerId = resolved;
                    if (source == WorkerIdSource.MachineNameHash)
                    {
                        OrionKeyDiagnostics.WarnAutoWorkerIdOnce(workerId);
                    }
                }
                snowflake = new SnowflakeIdGenerator(workerId, options.SnowflakeEpoch);
            }
            return snowflake;
        }
    }

    /// <summary>Resets process state. For <c>OrionKey.Testing</c> only; not part of the public contract.</summary>
    internal static void ResetForTesting()
    {
        lock (Gate)
        {
            options = new OrionKeyOptions();
            snowflake = null;
            configured = false;
        }
    }
}
```

Add `[assembly: InternalsVisibleTo("Moongazing.OrionKey.Testing")]` and `[assembly: InternalsVisibleTo("Moongazing.OrionKey.Tests")]` — create `src/Moongazing.OrionKey/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Moongazing.OrionKey.Testing")]
[assembly: InternalsVisibleTo("Moongazing.OrionKey.Tests")]
```

- [ ] **Step 6: Delete the placeholder, run tests, expect PASS**

Delete `src/Moongazing.OrionKey/_Placeholder.cs`.
Run: `dotnet test tests/Moongazing.OrionKey.Tests`
Expected: all tests pass (Tasks 2-7 suites).

- [ ] **Step 7: Commit**

```
git add src/Moongazing.OrionKey tests/Moongazing.OrionKey.Tests
git commit -m "feat(orionkey): OrionId attribute, strategy markers, OrionKey facade"
```

---

## Task 8: Generator project — attribute detection skeleton

**Files:**
- Create: `src/Moongazing.OrionKey.Generators/OrionIdGenerator.cs`
- Create: `src/Moongazing.OrionKey.Generators/Model/ValueType.cs`
- Create: `src/Moongazing.OrionKey.Generators/Model/StrategyType.cs`
- Delete: `src/Moongazing.OrionKey.Generators/_Placeholder.cs`
- Test: `tests/Moongazing.OrionKey.Generators.Tests/GeneratorHarness.cs`
- Test: `tests/Moongazing.OrionKey.Generators.Tests/DetectionTests.cs`

> **Background for the implementer:** A Roslyn incremental generator is registered via `[Generator]` + `IIncrementalGenerator`. Use `context.SyntaxProvider.ForAttributeWithMetadataName` to find decorated types. Generic attributes have arity-suffixed metadata names: `OrionIdAttribute<TValue>` is `Moongazing.OrionKey.OrionIdAttribute`1` and the two-arg form is `Moongazing.OrionKey.OrionIdAttribute`2`. Register a provider for **each**.

- [ ] **Step 1: Create the enums**

`Model/ValueType.cs`:

```csharp
namespace Moongazing.OrionKey.Generators.Model;

internal enum ValueType
{
    Guid,
    Int32,
    Int64,
    String,
}
```

`Model/StrategyType.cs`:

```csharp
namespace Moongazing.OrionKey.Generators.Model;

internal enum StrategyType
{
    /// <summary>No strategy supplied. Guid -> Guid.NewGuid(); int/long -> externally assigned.</summary>
    None,
    Snowflake,
    Ulid,
    NanoId,
    GuidV7,
}
```

- [ ] **Step 2: Create the generator harness for tests**

`tests/Moongazing.OrionKey.Generators.Tests/GeneratorHarness.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Moongazing.OrionKey.Generators;

namespace Moongazing.OrionKey.Generators.Tests;

/// <summary>Runs OrionIdGenerator against a source string and exposes the result.</summary>
internal static class GeneratorHarness
{
    public static GeneratorRunResult Run(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create(
            "GeneratorTestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(new OrionIdGenerator())
            .RunGenerators(compilation);

        return driver.GetRunResult().Results.Single();
    }

    /// <summary>Concatenates every generated source for substring assertions.</summary>
    public static string AllGeneratedText(this GeneratorRunResult result)
        => string.Join("\n\n", result.GeneratedSources.Select(s => s.SourceText.ToString()));
}
```

The `.Generators.Tests` csproj must reference `Moongazing.OrionKey` (so `[OrionId]` resolves in test source) — confirm the ProjectReference from Task 1 Step 5 is present.

- [ ] **Step 3: Write the failing detection test**

`tests/Moongazing.OrionKey.Generators.Tests/DetectionTests.cs`:

```csharp
namespace Moongazing.OrionKey.Generators.Tests;

public class DetectionTests
{
    [Fact]
    public void Generator_ShouldEmitSource_ForOneArgOrionIdStruct()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>]
            public readonly partial struct OrderId;
            """;

        var result = GeneratorHarness.Run(source);

        Assert.NotEmpty(result.GeneratedSources);
        Assert.Contains("struct OrderId", result.AllGeneratedText());
    }

    [Fact]
    public void Generator_ShouldEmitSource_ForTwoArgOrionIdStruct()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>]
            public readonly partial struct UserId;
            """;

        var result = GeneratorHarness.Run(source);

        Assert.Contains("struct UserId", result.AllGeneratedText());
    }

    [Fact]
    public void Generator_ShouldEmitNothing_ForUndecoratedStruct()
    {
        const string source = """
            namespace Demo;
            public readonly partial struct Plain;
            """;

        var result = GeneratorHarness.Run(source);

        Assert.Empty(result.GeneratedSources);
    }
}
```

- [ ] **Step 4: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests --filter DetectionTests`
Expected: build error — `OrionIdGenerator` does not exist.

- [ ] **Step 5: Create the generator skeleton**

`src/Moongazing.OrionKey.Generators/OrionIdGenerator.cs`:

```csharp
using Microsoft.CodeAnalysis;

namespace Moongazing.OrionKey.Generators;

/// <summary>
/// Incremental generator that turns <c>[OrionId]</c>-decorated structs into fully-featured
/// strongly-typed ids.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class OrionIdGenerator : IIncrementalGenerator
{
    private const string OneArgAttribute = "Moongazing.OrionKey.OrionIdAttribute`1";
    private const string TwoArgAttribute = "Moongazing.OrionKey.OrionIdAttribute`2";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var oneArg = context.SyntaxProvider.ForAttributeWithMetadataName(
            OneArgAttribute,
            predicate: static (node, _) => true,
            transform: static (ctx, _) => ctx.TargetSymbol as INamedTypeSymbol);

        var twoArg = context.SyntaxProvider.ForAttributeWithMetadataName(
            TwoArgAttribute,
            predicate: static (node, _) => true,
            transform: static (ctx, _) => ctx.TargetSymbol as INamedTypeSymbol);

        // Skeleton: emit a marker file per detected struct. Replaced in Tasks 9-16.
        context.RegisterSourceOutput(oneArg, static (spc, symbol) => EmitSkeleton(spc, symbol));
        context.RegisterSourceOutput(twoArg, static (spc, symbol) => EmitSkeleton(spc, symbol));
    }

    private static void EmitSkeleton(SourceProductionContext spc, INamedTypeSymbol? symbol)
    {
        if (symbol is null)
        {
            return;
        }
        var ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();
        var nsBlock = ns.Length == 0 ? string.Empty : $"namespace {ns};\n\n";
        spc.AddSource(
            $"{symbol.Name}.OrionId.g.cs",
            $"// <auto-generated/>\n{nsBlock}readonly partial struct {symbol.Name} {{ }}\n");
    }
}
```

- [ ] **Step 6: Delete the generator placeholder, run tests, expect PASS**

Delete `src/Moongazing.OrionKey.Generators/_Placeholder.cs`.
Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests --filter DetectionTests`
Expected: 3 tests pass.

- [ ] **Step 7: Commit**

```
git add src/Moongazing.OrionKey.Generators tests/Moongazing.OrionKey.Generators.Tests
git commit -m "feat(orionkey): generator skeleton detecting [OrionId] structs"
```

---

## Task 9: Parsing — `OrionIdModel` and `OrionIdParser`

**Files:**
- Create: `src/Moongazing.OrionKey.Generators/Model/OrionIdModel.cs`
- Create: `src/Moongazing.OrionKey.Generators/Parsing/OrionIdParser.cs`
- Test: `tests/Moongazing.OrionKey.Generators.Tests/ParserTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/Moongazing.OrionKey.Generators.Tests/ParserTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Moongazing.OrionKey.Generators.Model;
using Moongazing.OrionKey.Generators.Parsing;

namespace Moongazing.OrionKey.Generators.Tests;

public class ParserTests
{
    private static INamedTypeSymbol FirstStruct(string source, out Compilation compilation)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location));
        compilation = CSharpCompilation.Create("ParseTest", new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var root = tree.GetRoot();
        var structSyntax = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.StructDeclarationSyntax>().First();
        return (INamedTypeSymbol)compilation.GetSemanticModel(tree).GetDeclaredSymbol(structSyntax)!;
    }

    [Fact]
    public void Parse_ShouldResolveGuidValueType_AndNoneStrategy()
    {
        var symbol = FirstStruct("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct OrderId;
            """, out _);

        var ok = OrionIdParser.TryParse(symbol, out var model, out var diagnostics);

        Assert.True(ok);
        Assert.Equal(ValueType.Guid, model!.ValueType);
        Assert.Equal(StrategyType.None, model.Strategy);
        Assert.Equal("OrderId", model.Name);
        Assert.Equal("Demo", model.Namespace);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Parse_ShouldResolveSnowflakeStrategy()
    {
        var symbol = FirstStruct("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct UserId;
            """, out _);

        var ok = OrionIdParser.TryParse(symbol, out var model, out _);

        Assert.True(ok);
        Assert.Equal(ValueType.Int64, model!.ValueType);
        Assert.Equal(StrategyType.Snowflake, model.Strategy);
        Assert.True(model.GeneratesNew);
        Assert.True(model.IsSortable);
    }

    [Fact]
    public void Parse_ShouldMarkIntAsExternallyAssigned()
    {
        var symbol = FirstStruct("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<int>] public readonly partial struct LineId;
            """, out _);

        var ok = OrionIdParser.TryParse(symbol, out var model, out _);

        Assert.True(ok);
        Assert.False(model!.GeneratesNew);
        Assert.False(model.IsSortable);
    }
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests --filter ParserTests`
Expected: build error.

- [ ] **Step 3: Create `Model/OrionIdModel.cs`**

```csharp
namespace Moongazing.OrionKey.Generators.Model;

/// <summary>Immutable parsed description of one <c>[OrionId]</c>-decorated struct.</summary>
internal sealed record OrionIdModel(
    string Name,
    string Namespace,
    ValueType ValueType,
    StrategyType Strategy)
{
    /// <summary>True when the struct should get a static <c>New()</c> factory.</summary>
    public bool GeneratesNew => Strategy != StrategyType.None || ValueType == ValueType.Guid;

    /// <summary>True when the strategy produces creation-ordered ids (gets <c>IComparable</c>).</summary>
    public bool IsSortable => Strategy is StrategyType.Snowflake or StrategyType.Ulid or StrategyType.GuidV7;

    /// <summary>The fully-qualified C# keyword/type of the underlying value.</summary>
    public string ValueKeyword => ValueType switch
    {
        ValueType.Guid => "global::System.Guid",
        ValueType.Int32 => "int",
        ValueType.Int64 => "long",
        ValueType.String => "string",
        _ => throw new System.ArgumentOutOfRangeException(nameof(ValueType)),
    };
}
```

- [ ] **Step 4: Create `Parsing/OrionIdParser.cs`**

```csharp
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Moongazing.OrionKey.Generators.Diagnostics;
using Moongazing.OrionKey.Generators.Model;

namespace Moongazing.OrionKey.Generators.Parsing;

/// <summary>Turns an <c>[OrionId]</c>-decorated type symbol into an <see cref="OrionIdModel"/>.</summary>
internal static class OrionIdParser
{
    public static bool TryParse(
        INamedTypeSymbol symbol,
        out OrionIdModel? model,
        out IReadOnlyList<Diagnostic> diagnostics)
    {
        var diags = new List<Diagnostic>();
        model = null;
        diagnostics = diags;

        var attribute = FindOrionIdAttribute(symbol);
        if (attribute is null)
        {
            return false;
        }

        var typeArgs = attribute.AttributeClass!.TypeArguments;
        var valueSymbol = typeArgs[0];
        var strategySymbol = typeArgs.Length == 2 ? typeArgs[1] : null;

        if (!TryMapValueType(valueSymbol, out var valueType))
        {
            diags.Add(Diagnostic.Create(OrionKeyDiagnostics.UnsupportedValueType,
                symbol.Locations[0], valueSymbol.ToDisplayString()));
            return false;
        }

        var strategy = StrategyType.None;
        if (strategySymbol is not null && !TryMapStrategy(strategySymbol, out strategy))
        {
            diags.Add(Diagnostic.Create(OrionKeyDiagnostics.UnsupportedValueType,
                symbol.Locations[0], strategySymbol.ToDisplayString()));
            return false;
        }

        // string requires an explicit strategy.
        if (valueType == ValueType.String && strategy == StrategyType.None)
        {
            diags.Add(Diagnostic.Create(OrionKeyDiagnostics.StringRequiresStrategy,
                symbol.Locations[0], symbol.Name));
            return false;
        }

        // strategy must be compatible with the value type.
        if (!IsCompatible(valueType, strategy))
        {
            diags.Add(Diagnostic.Create(OrionKeyDiagnostics.IncompatibleStrategy,
                symbol.Locations[0], strategy.ToString(), valueType.ToString()));
            return false;
        }

        var ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();

        model = new OrionIdModel(symbol.Name, ns, valueType, strategy);
        return true;
    }

    private static AttributeData? FindOrionIdAttribute(INamedTypeSymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var name = attr.AttributeClass?.ConstructUnboundGenericType().ToDisplayString();
            if (name is "Moongazing.OrionKey.OrionIdAttribute<>"
                     or "Moongazing.OrionKey.OrionIdAttribute<,>")
            {
                return attr;
            }
        }
        return null;
    }

    private static bool TryMapValueType(ITypeSymbol symbol, out ValueType valueType)
    {
        valueType = default;
        switch (symbol.SpecialType)
        {
            case SpecialType.System_Int32: valueType = ValueType.Int32; return true;
            case SpecialType.System_Int64: valueType = ValueType.Int64; return true;
            case SpecialType.System_String: valueType = ValueType.String; return true;
        }
        if (symbol.ToDisplayString() == "System.Guid")
        {
            valueType = ValueType.Guid;
            return true;
        }
        return false;
    }

    private static bool TryMapStrategy(ITypeSymbol symbol, out StrategyType strategy)
    {
        strategy = symbol.ToDisplayString() switch
        {
            "Moongazing.OrionKey.Snowflake" => StrategyType.Snowflake,
            "Moongazing.OrionKey.Ulid" => StrategyType.Ulid,
            "Moongazing.OrionKey.NanoId" => StrategyType.NanoId,
            "Moongazing.OrionKey.GuidV7" => StrategyType.GuidV7,
            _ => StrategyType.None,
        };
        // None here means an unknown strategy symbol was supplied.
        return strategy != StrategyType.None;
    }

    private static bool IsCompatible(ValueType value, StrategyType strategy) => strategy switch
    {
        StrategyType.None => value is ValueType.Guid or ValueType.Int32 or ValueType.Int64,
        StrategyType.Snowflake => value == ValueType.Int64,
        StrategyType.Ulid => value == ValueType.String,
        StrategyType.NanoId => value == ValueType.String,
        StrategyType.GuidV7 => value == ValueType.Guid,
        _ => false,
    };
}
```

- [ ] **Step 4b: Add a minimal `OrionKeyDiagnostics` so the parser compiles**

The parser references `OrionKeyDiagnostics.UnsupportedValueType` etc. Create `src/Moongazing.OrionKey.Generators/Diagnostics/OrionKeyDiagnostics.cs` now with the three descriptors used above (the full set ORIONKEY001-005 is finished in Task 10):

```csharp
using Microsoft.CodeAnalysis;

namespace Moongazing.OrionKey.Generators.Diagnostics;

internal static class OrionKeyDiagnostics
{
    private const string Category = "OrionKey";

    public static readonly DiagnosticDescriptor NotReadonlyPartialStruct = new(
        "ORIONKEY001", "OrionId target must be a readonly partial struct",
        "'{0}' is marked [OrionId] but is not a 'readonly partial struct'",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedValueType = new(
        "ORIONKEY002", "Unsupported OrionId value or strategy type",
        "'{0}' is not a supported [OrionId] value type or strategy",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor StringRequiresStrategy = new(
        "ORIONKEY003", "string OrionId requires an explicit strategy",
        "'{0}' uses a string value type, which requires an explicit strategy (Ulid or NanoId)",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IncompatibleStrategy = new(
        "ORIONKEY004", "Incompatible OrionId strategy",
        "Strategy '{0}' is not compatible with value type '{1}'",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MemberCollision = new(
        "ORIONKEY005", "OrionId struct declares a generated member",
        "'{0}' declares a member named '{1}' that the OrionId generator also emits",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
```

- [ ] **Step 5: Run tests, expect PASS**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests --filter ParserTests`
Expected: 3 tests pass.

- [ ] **Step 6: Commit**

```
git add src/Moongazing.OrionKey.Generators tests/Moongazing.OrionKey.Generators.Tests/ParserTests.cs
git commit -m "feat(orionkey): OrionId model + parser with value/strategy resolution"
```

---

## Task 10: Diagnostics — wire ORIONKEY001-005 into the generator

**Files:**
- Modify: `src/Moongazing.OrionKey.Generators/OrionIdGenerator.cs`
- Modify: `src/Moongazing.OrionKey.Generators/Parsing/OrionIdParser.cs` (add the readonly/partial check)
- Test: `tests/Moongazing.OrionKey.Generators.Tests/DiagnosticsTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/Moongazing.OrionKey.Generators.Tests/DiagnosticsTests.cs`:

```csharp
using System.Linq;

namespace Moongazing.OrionKey.Generators.Tests;

public class DiagnosticsTests
{
    private static string[] DiagnosticIds(string source)
        => GeneratorHarness.Run(source).Diagnostics.Select(d => d.Id).ToArray();

    [Fact]
    public void ORIONKEY001_ShouldFire_WhenTargetIsNotReadonlyPartialStruct()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public partial struct OrderId;
            """;
        Assert.Contains("ORIONKEY001", DiagnosticIds(source));
    }

    [Fact]
    public void ORIONKEY002_ShouldFire_WhenValueTypeUnsupported()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<double>] public readonly partial struct OrderId;
            """;
        Assert.Contains("ORIONKEY002", DiagnosticIds(source));
    }

    [Fact]
    public void ORIONKEY003_ShouldFire_WhenStringHasNoStrategy()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string>] public readonly partial struct TenantId;
            """;
        Assert.Contains("ORIONKEY003", DiagnosticIds(source));
    }

    [Fact]
    public void ORIONKEY004_ShouldFire_WhenStrategyIncompatibleWithValueType()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid, Snowflake>] public readonly partial struct OrderId;
            """;
        Assert.Contains("ORIONKEY004", DiagnosticIds(source));
    }

    [Fact]
    public void NoDiagnostics_ForValidDeclaration()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct UserId;
            """;
        Assert.Empty(DiagnosticIds(source));
    }
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests --filter DiagnosticsTests`
Expected: failures — diagnostics not reported yet (skeleton ignores them).

- [ ] **Step 3: Add the readonly/partial check to `OrionIdParser.TryParse`**

At the start of `TryParse`, after `FindOrionIdAttribute` succeeds, before mapping value types, add:

```csharp
        if (!IsReadonlyPartialStruct(symbol))
        {
            diags.Add(Diagnostic.Create(OrionKeyDiagnostics.NotReadonlyPartialStruct,
                symbol.Locations[0], symbol.Name));
            return false;
        }
```

And add the helper:

```csharp
    private static bool IsReadonlyPartialStruct(INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind != TypeKind.Struct || !symbol.IsReadOnly)
        {
            return false;
        }
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.StructDeclarationSyntax s
                && s.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
            {
                return true;
            }
        }
        return false;
    }
```

- [ ] **Step 4: Rewrite the generator to parse + report diagnostics**

Replace the body of `OrionIdGenerator.Initialize`'s `RegisterSourceOutput` calls. Both providers feed a shared handler:

```csharp
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var oneArg = context.SyntaxProvider.ForAttributeWithMetadataName(
            OneArgAttribute, static (_, _) => true,
            static (ctx, _) => ctx.TargetSymbol as INamedTypeSymbol);

        var twoArg = context.SyntaxProvider.ForAttributeWithMetadataName(
            TwoArgAttribute, static (_, _) => true,
            static (ctx, _) => ctx.TargetSymbol as INamedTypeSymbol);

        context.RegisterSourceOutput(oneArg, static (spc, symbol) => Handle(spc, symbol));
        context.RegisterSourceOutput(twoArg, static (spc, symbol) => Handle(spc, symbol));
    }

    private static void Handle(SourceProductionContext spc, INamedTypeSymbol? symbol)
    {
        if (symbol is null)
        {
            return;
        }

        if (!OrionIdParser.TryParse(symbol, out var model, out var diagnostics))
        {
            foreach (var diagnostic in diagnostics)
            {
                spc.ReportDiagnostic(diagnostic);
            }
            return;
        }

        // Emission added in Tasks 11-16. For now, emit the skeleton body so Detection tests pass.
        var nsBlock = model!.Namespace.Length == 0
            ? string.Empty
            : $"namespace {model.Namespace};\n\n";
        spc.AddSource(
            $"{model.Name}.OrionId.g.cs",
            $"// <auto-generated/>\n{nsBlock}readonly partial struct {model.Name} {{ }}\n");
    }
```

Add the needed `using Moongazing.OrionKey.Generators.Parsing;`.

- [ ] **Step 5: Run tests, expect PASS**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests`
Expected: `DetectionTests`, `ParserTests`, `DiagnosticsTests` all pass.

- [ ] **Step 6: Commit**

```
git add src/Moongazing.OrionKey.Generators tests/Moongazing.OrionKey.Generators.Tests/DiagnosticsTests.cs
git commit -m "feat(orionkey): ORIONKEY001-004 diagnostics wired into the generator"
```

---

## Task 11: Core body emitter — `Value`, ctor, equality, `New()`

**Files:**
- Create: `src/Moongazing.OrionKey.Generators/Emit/CoreBodyEmitter.cs`
- Modify: `src/Moongazing.OrionKey.Generators/OrionIdGenerator.cs`
- Test: `tests/Moongazing.OrionKey.Generators.Tests/CoreBodyTests.cs`

> **Target emitted output.** For `[OrionId<long, Snowflake>] public readonly partial struct UserId` in namespace `Demo`, the core body file `UserId.OrionId.g.cs` must contain a partial struct equivalent to:
>
> ```csharp
> // <auto-generated/>
> #nullable enable
> namespace Demo;
>
> readonly partial struct UserId : global::System.IEquatable<UserId>
> {
>     public long Value { get; }
>     public UserId(long value) => Value = value;
>     public static readonly UserId Empty = new(default);
>     public static UserId New() => new(global::Moongazing.OrionKey.OrionKey.NextSnowflake());
>     public bool Equals(UserId other) => Value.Equals(other.Value);
>     public override bool Equals(object? obj) => obj is UserId other && Equals(other);
>     public override int GetHashCode() => Value.GetHashCode();
>     public override string ToString() => Value.ToString();
>     public static bool operator ==(UserId left, UserId right) => left.Equals(right);
>     public static bool operator !=(UserId left, UserId right) => !left.Equals(right);
> }
> ```
>
> `New()` body per strategy/value:
> - `Guid` (no strategy): `new(global::System.Guid.NewGuid())`
> - `GuidV7`: `new(global::Moongazing.OrionKey.OrionKey.NewGuidV7())`
> - `Snowflake`: `new(global::Moongazing.OrionKey.OrionKey.NextSnowflake())`
> - `Ulid`: `new(global::Moongazing.OrionKey.OrionKey.NewUlid())`
> - `NanoId`: `new(global::Moongazing.OrionKey.OrionKey.NewNanoId())`
> - `int`/`long` externally assigned (`GeneratesNew == false`): emit no `New()` method.
>
> For `string`-valued ids, `Value` is non-nullable `string` and `Empty` uses `new(string.Empty)`; `ToString()` returns `Value`. For `string` types add `[System.Diagnostics.CodeAnalysis.NotNull]` discipline as needed to stay warning-clean under nullable.

- [ ] **Step 1: Write the failing tests**

`tests/Moongazing.OrionKey.Generators.Tests/CoreBodyTests.cs`:

```csharp
namespace Moongazing.OrionKey.Generators.Tests;

public class CoreBodyTests
{
    private static string Generate(string attribute, string structName, string valueType = "")
    {
        var source = $$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{structName}};
            """;
        return GeneratorHarness.Run(source).AllGeneratedText();
    }

    [Fact]
    public void Emits_ValueProperty_AndConstructor_ForGuid()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("public global::System.Guid Value { get; }", output);
        Assert.Contains("public OrderId(global::System.Guid value)", output);
    }

    [Fact]
    public void Emits_IEquatable_AndOperators()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("global::System.IEquatable<OrderId>", output);
        Assert.Contains("operator ==(OrderId", output);
        Assert.Contains("operator !=(OrderId", output);
        Assert.Contains("public override int GetHashCode()", output);
    }

    [Fact]
    public void Emits_New_ForGuid_UsingGuidNewGuid()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("public static OrderId New() => new(global::System.Guid.NewGuid());", output);
    }

    [Fact]
    public void Emits_New_ForSnowflake_DelegatingToFacade()
    {
        var output = Generate("OrionId<long, Snowflake>", "UserId");
        Assert.Contains("global::Moongazing.OrionKey.OrionKey.NextSnowflake()", output);
    }

    [Fact]
    public void DoesNotEmit_New_ForExternallyAssignedInt()
    {
        var output = Generate("OrionId<int>", "LineId");
        Assert.DoesNotContain("New()", output);
    }
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests --filter CoreBodyTests`
Expected: failures — the skeleton emits an empty body.

- [ ] **Step 3: Create `Emit/CoreBodyEmitter.cs`**

```csharp
using System.Text;
using Moongazing.OrionKey.Generators.Model;

namespace Moongazing.OrionKey.Generators.Emit;

/// <summary>Emits the core partial struct body: Value, ctor, Empty, New(), equality.</summary>
internal static class CoreBodyEmitter
{
    public static string Emit(OrionIdModel model)
    {
        var name = model.Name;
        var value = model.ValueKeyword;
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        if (model.Namespace.Length != 0)
        {
            sb.AppendLine($"namespace {model.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"readonly partial struct {name} : global::System.IEquatable<{name}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public {value} Value {{ get; }}");
        sb.AppendLine($"    public {name}({value} value) => Value = value;");

        var emptyArg = model.ValueType == ValueType.String ? "string.Empty" : "default";
        sb.AppendLine($"    public static readonly {name} Empty = new({emptyArg});");

        if (model.GeneratesNew)
        {
            sb.AppendLine($"    public static {name} New() => new({NewExpression(model)});");
        }

        sb.AppendLine($"    public bool Equals({name} other) => Value.Equals(other.Value);");
        sb.AppendLine($"    public override bool Equals(object? obj) => obj is {name} other && Equals(other);");
        sb.AppendLine("    public override int GetHashCode() => Value.GetHashCode();");

        var toStr = model.ValueType == ValueType.String ? "Value" : "Value.ToString()";
        sb.AppendLine($"    public override string ToString() => {toStr};");

        sb.AppendLine($"    public static bool operator ==({name} left, {name} right) => left.Equals(right);");
        sb.AppendLine($"    public static bool operator !=({name} left, {name} right) => !left.Equals(right);");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string NewExpression(OrionIdModel model) => model.Strategy switch
    {
        StrategyType.Snowflake => "global::Moongazing.OrionKey.OrionKey.NextSnowflake()",
        StrategyType.Ulid => "global::Moongazing.OrionKey.OrionKey.NewUlid()",
        StrategyType.NanoId => "global::Moongazing.OrionKey.OrionKey.NewNanoId()",
        StrategyType.GuidV7 => "global::Moongazing.OrionKey.OrionKey.NewGuidV7()",
        StrategyType.None when model.ValueType == ValueType.Guid => "global::System.Guid.NewGuid()",
        _ => throw new System.InvalidOperationException("New() requested for a non-generating model."),
    };
}
```

- [ ] **Step 4: Wire it into the generator**

In `OrionIdGenerator.Handle`, replace the skeleton `AddSource` with:

```csharp
        spc.AddSource($"{model!.Name}.OrionId.g.cs", CoreBodyEmitter.Emit(model));
```

Add `using Moongazing.OrionKey.Generators.Emit;`.

- [ ] **Step 5: Run tests, expect PASS**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests`
Expected: all generator tests pass.

- [ ] **Step 6: Commit**

```
git add src/Moongazing.OrionKey.Generators tests/Moongazing.OrionKey.Generators.Tests/CoreBodyTests.cs
git commit -m "feat(orionkey): core body emitter — Value, ctor, equality, New()"
```

---

## Task 12: `IComparable` emitter for sortable strategies

**Files:**
- Create: `src/Moongazing.OrionKey.Generators/Emit/ComparableEmitter.cs`
- Modify: `src/Moongazing.OrionKey.Generators/OrionIdGenerator.cs`
- Test: `tests/Moongazing.OrionKey.Generators.Tests/ComparableTests.cs`

> **Target output.** For a sortable model (`Snowflake`/`Ulid`/`GuidV7`), emit a second partial declaration adding `global::System.IComparable<Name>` and `global::System.IComparable`, plus `CompareTo` and the four ordering operators. Non-sortable models (`Guid` no-strategy, `NanoId`, `int`/`long` externally assigned) emit nothing from this emitter.

- [ ] **Step 1: Write the failing tests**

```csharp
namespace Moongazing.OrionKey.Generators.Tests;

public class ComparableTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_IComparable_ForSnowflake()
    {
        var output = Generate("OrionId<long, Snowflake>", "UserId");
        Assert.Contains("global::System.IComparable<UserId>", output);
        Assert.Contains("public int CompareTo(UserId other)", output);
        Assert.Contains("operator <(UserId", output);
        Assert.Contains("operator >=(UserId", output);
    }

    [Fact]
    public void Emits_IComparable_ForUlid()
    {
        var output = Generate("OrionId<string, Ulid>", "TenantId");
        Assert.Contains("public int CompareTo(TenantId other)", output);
    }

    [Fact]
    public void DoesNotEmit_IComparable_ForNanoId()
    {
        var output = Generate("OrionId<string, NanoId>", "SessionId");
        Assert.DoesNotContain("CompareTo", output);
    }

    [Fact]
    public void DoesNotEmit_IComparable_ForPlainGuid()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.DoesNotContain("CompareTo", output);
    }
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests --filter ComparableTests`
Expected: failures.

- [ ] **Step 3: Create `Emit/ComparableEmitter.cs`**

```csharp
using System.Text;
using Moongazing.OrionKey.Generators.Model;

namespace Moongazing.OrionKey.Generators.Emit;

/// <summary>Emits IComparable + ordering operators for sortable id strategies.</summary>
internal static class ComparableEmitter
{
    /// <summary>Returns the comparable partial, or null when the model is not sortable.</summary>
    public static string? Emit(OrionIdModel model)
    {
        if (!model.IsSortable)
        {
            return null;
        }

        var name = model.Name;
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        if (model.Namespace.Length != 0)
        {
            sb.AppendLine($"namespace {model.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"readonly partial struct {name} : "
                    + $"global::System.IComparable<{name}>, global::System.IComparable");
        sb.AppendLine("{");
        sb.AppendLine($"    public int CompareTo({name} other) => "
                    + "global::System.Collections.Generic.Comparer<"
                    + $"{model.ValueKeyword}>.Default.Compare(Value, other.Value);");
        sb.AppendLine("    public int CompareTo(object? obj) => obj switch");
        sb.AppendLine("    {");
        sb.AppendLine("        null => 1,");
        sb.AppendLine($"        {name} other => CompareTo(other),");
        sb.AppendLine("        _ => throw new global::System.ArgumentException("
                    + $"\"Object must be of type {name}.\", nameof(obj)),");
        sb.AppendLine("    };");
        sb.AppendLine($"    public static bool operator <({name} left, {name} right) => left.CompareTo(right) < 0;");
        sb.AppendLine($"    public static bool operator <=({name} left, {name} right) => left.CompareTo(right) <= 0;");
        sb.AppendLine($"    public static bool operator >({name} left, {name} right) => left.CompareTo(right) > 0;");
        sb.AppendLine($"    public static bool operator >=({name} left, {name} right) => left.CompareTo(right) >= 0;");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Wire it in**

In `OrionIdGenerator.Handle`, after emitting the core body:

```csharp
        var comparable = ComparableEmitter.Emit(model);
        if (comparable is not null)
        {
            spc.AddSource($"{model.Name}.OrionId.Comparable.g.cs", comparable);
        }
```

- [ ] **Step 5: Run tests, expect PASS**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests`
Expected: all pass.

- [ ] **Step 6: Commit**

```
git add src/Moongazing.OrionKey.Generators tests/Moongazing.OrionKey.Generators.Tests/ComparableTests.cs
git commit -m "feat(orionkey): IComparable emitter for sortable strategies"
```

---

## Task 13: `System.Text.Json` converter emitter

**Files:**
- Create: `src/Moongazing.OrionKey.Generators/Emit/JsonConverterEmitter.cs`
- Modify: `src/Moongazing.OrionKey.Generators/OrionIdGenerator.cs`
- Test: `tests/Moongazing.OrionKey.Generators.Tests/JsonConverterTests.cs`

> **Target output.** Emit a `[JsonConverter]`-attached nested or sibling converter so the id serializes as its underlying primitive. Emit a third partial that decorates the struct with `[global::System.Text.Json.Serialization.JsonConverter(typeof(NameJsonConverter))]` and a `NameJsonConverter : global::System.Text.Json.Serialization.JsonConverter<Name>`. `Read` calls the matching `Utf8JsonReader` getter (`GetGuid`/`GetInt32`/`GetInt64`/`GetString`); `Write` calls the matching `Utf8JsonWriter.WriteX` (`WriteStringValue` for Guid/string, `WriteNumberValue` for int/long).

- [ ] **Step 1: Write the failing tests**

```csharp
namespace Moongazing.OrionKey.Generators.Tests;

public class JsonConverterTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_JsonConverterAttribute_OnStruct()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("System.Text.Json.Serialization.JsonConverter(typeof(OrderIdJsonConverter))", output);
    }

    [Fact]
    public void Emits_JsonConverterClass()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("class OrderIdJsonConverter", output);
        Assert.Contains("JsonConverter<OrderId>", output);
    }

    [Fact]
    public void Json_ForLong_UsesNumberApis()
    {
        var output = Generate("OrionId<long, Snowflake>", "UserId");
        Assert.Contains("GetInt64()", output);
        Assert.Contains("WriteNumberValue(", output);
    }

    [Fact]
    public void Json_ForString_UsesStringApis()
    {
        var output = Generate("OrionId<string, Ulid>", "TenantId");
        Assert.Contains("GetString()", output);
        Assert.Contains("WriteStringValue(", output);
    }
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests --filter JsonConverterTests`
Expected: failures.

- [ ] **Step 3: Create `Emit/JsonConverterEmitter.cs`**

```csharp
using System.Text;
using Moongazing.OrionKey.Generators.Model;

namespace Moongazing.OrionKey.Generators.Emit;

/// <summary>Emits a System.Text.Json converter and attaches it to the id struct.</summary>
internal static class JsonConverterEmitter
{
    public static string Emit(OrionIdModel model)
    {
        var name = model.Name;
        var value = model.ValueKeyword;
        var converter = $"{name}JsonConverter";

        var (readExpr, writeStmt) = model.ValueType switch
        {
            ValueType.Guid => ("reader.GetGuid()", "writer.WriteStringValue(value.Value)"),
            ValueType.Int32 => ("reader.GetInt32()", "writer.WriteNumberValue(value.Value)"),
            ValueType.Int64 => ("reader.GetInt64()", "writer.WriteNumberValue(value.Value)"),
            ValueType.String => ("reader.GetString()!", "writer.WriteStringValue(value.Value)"),
            _ => throw new System.ArgumentOutOfRangeException(nameof(model)),
        };

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        if (model.Namespace.Length != 0)
        {
            sb.AppendLine($"namespace {model.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"[global::System.Text.Json.Serialization.JsonConverter(typeof({converter}))]");
        sb.AppendLine($"readonly partial struct {name} {{ }}");
        sb.AppendLine();
        sb.AppendLine($"file sealed class {converter} "
                    + $": global::System.Text.Json.Serialization.JsonConverter<{name}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public override {name} Read("
                    + "ref global::System.Text.Json.Utf8JsonReader reader, "
                    + "global::System.Type typeToConvert, "
                    + "global::System.Text.Json.JsonSerializerOptions options) "
                    + $"=> new({readExpr});");
        sb.AppendLine($"    public override void Write("
                    + "global::System.Text.Json.Utf8JsonWriter writer, "
                    + $"{name} value, "
                    + "global::System.Text.Json.JsonSerializerOptions options) "
                    + $"=> {writeStmt};");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
```

> Note: the converter is a `file`-scoped class so multiple generated ids never collide. The `[JsonConverter(typeof(...))]` attribute on a `file` type works because the attribute application is in the same file.

- [ ] **Step 4: Wire it in**

In `OrionIdGenerator.Handle`, after the comparable emission:

```csharp
        spc.AddSource($"{model.Name}.OrionId.Json.g.cs", JsonConverterEmitter.Emit(model));
```

- [ ] **Step 5: Run tests, expect PASS**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests`
Expected: all pass.

- [ ] **Step 6: Commit**

```
git add src/Moongazing.OrionKey.Generators tests/Moongazing.OrionKey.Generators.Tests/JsonConverterTests.cs
git commit -m "feat(orionkey): System.Text.Json converter emitter"
```

---

## Task 14: `TypeConverter` emitter

**Files:**
- Create: `src/Moongazing.OrionKey.Generators/Emit/TypeConverterEmitter.cs`
- Modify: `src/Moongazing.OrionKey.Generators/OrionIdGenerator.cs`
- Test: `tests/Moongazing.OrionKey.Generators.Tests/TypeConverterTests.cs`

> **Target output.** Emit a partial decorating the struct with `[global::System.ComponentModel.TypeConverter(typeof(NameTypeConverter))]` and a `file sealed class NameTypeConverter : global::System.ComponentModel.TypeConverter` overriding `CanConvertFrom`/`ConvertFrom` (from `string`) and `CanConvertTo`/`ConvertTo` (to `string`). For Guid parse via `global::System.Guid.Parse`, for int/long via `int.Parse`/`long.Parse` with `InvariantCulture`, for string pass through.

- [ ] **Step 1: Write the failing tests**

```csharp
namespace Moongazing.OrionKey.Generators.Tests;

public class TypeConverterTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_TypeConverterAttribute()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("System.ComponentModel.TypeConverter(typeof(OrderIdTypeConverter))", output);
    }

    [Fact]
    public void Emits_TypeConverterClass_WithConvertFromAndTo()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("class OrderIdTypeConverter", output);
        Assert.Contains("CanConvertFrom", output);
        Assert.Contains("ConvertTo", output);
    }
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests --filter TypeConverterTests`
Expected: failures.

- [ ] **Step 3: Create `Emit/TypeConverterEmitter.cs`**

```csharp
using System.Text;
using Moongazing.OrionKey.Generators.Model;

namespace Moongazing.OrionKey.Generators.Emit;

/// <summary>Emits a TypeConverter for ASP.NET Core route/query/form binding.</summary>
internal static class TypeConverterEmitter
{
    public static string Emit(OrionIdModel model)
    {
        var name = model.Name;
        var converter = $"{name}TypeConverter";

        var parseExpr = model.ValueType switch
        {
            ValueType.Guid => "global::System.Guid.Parse(text)",
            ValueType.Int32 => "int.Parse(text, global::System.Globalization.CultureInfo.InvariantCulture)",
            ValueType.Int64 => "long.Parse(text, global::System.Globalization.CultureInfo.InvariantCulture)",
            ValueType.String => "text",
            _ => throw new System.ArgumentOutOfRangeException(nameof(model)),
        };

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        if (model.Namespace.Length != 0)
        {
            sb.AppendLine($"namespace {model.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"[global::System.ComponentModel.TypeConverter(typeof({converter}))]");
        sb.AppendLine($"readonly partial struct {name} {{ }}");
        sb.AppendLine();
        sb.AppendLine($"file sealed class {converter} : global::System.ComponentModel.TypeConverter");
        sb.AppendLine("{");
        sb.AppendLine("    public override bool CanConvertFrom("
                    + "global::System.ComponentModel.ITypeDescriptorContext? context, "
                    + "global::System.Type sourceType) "
                    + "=> sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);");
        sb.AppendLine("    public override object? ConvertFrom("
                    + "global::System.ComponentModel.ITypeDescriptorContext? context, "
                    + "global::System.Globalization.CultureInfo? culture, object value) "
                    + $"=> value is string text ? new {name}({parseExpr}) "
                    + ": base.ConvertFrom(context, culture, value);");
        sb.AppendLine("    public override bool CanConvertTo("
                    + "global::System.ComponentModel.ITypeDescriptorContext? context, "
                    + "global::System.Type? destinationType) "
                    + "=> destinationType == typeof(string) || base.CanConvertTo(context, destinationType);");
        sb.AppendLine("    public override object? ConvertTo("
                    + "global::System.ComponentModel.ITypeDescriptorContext? context, "
                    + "global::System.Globalization.CultureInfo? culture, object? value, "
                    + "global::System.Type destinationType) "
                    + $"=> destinationType == typeof(string) && value is {name} id "
                    + "? id.ToString() : base.ConvertTo(context, culture, value, destinationType);");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Wire it in**

```csharp
        spc.AddSource($"{model.Name}.OrionId.TypeConverter.g.cs", TypeConverterEmitter.Emit(model));
```

- [ ] **Step 5: Run tests, expect PASS**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests`
Expected: all pass.

- [ ] **Step 6: Commit**

```
git add src/Moongazing.OrionKey.Generators tests/Moongazing.OrionKey.Generators.Tests/TypeConverterTests.cs
git commit -m "feat(orionkey): TypeConverter emitter"
```

---

## Task 15: `IParsable` / `ISpanParsable` emitter

**Files:**
- Create: `src/Moongazing.OrionKey.Generators/Emit/ParsableEmitter.cs`
- Modify: `src/Moongazing.OrionKey.Generators/OrionIdGenerator.cs`
- Test: `tests/Moongazing.OrionKey.Generators.Tests/ParsableTests.cs`

> **Target output.** Emit a partial adding `: global::System.IParsable<Name>, global::System.ISpanParsable<Name>` and the four required members: `Parse(string, IFormatProvider?)`, `TryParse(string?, IFormatProvider?, out Name)`, `Parse(ReadOnlySpan<char>, IFormatProvider?)`, `TryParse(ReadOnlySpan<char>, IFormatProvider?, out Name)`. For Guid use `Guid.Parse`/`Guid.TryParse`; int/long use `int`/`long` `Parse`/`TryParse` with the provider; string wraps the input directly (`TryParse` always succeeds for non-null input).

- [ ] **Step 1: Write the failing tests**

```csharp
namespace Moongazing.OrionKey.Generators.Tests;

public class ParsableTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_IParsable_AndISpanParsable()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("global::System.IParsable<OrderId>", output);
        Assert.Contains("global::System.ISpanParsable<OrderId>", output);
    }

    [Fact]
    public void Emits_AllFourParseMembers()
    {
        var output = Generate("OrionId<long, Snowflake>", "UserId");
        Assert.Contains("static UserId global::System.IParsable<UserId>.Parse(", output);
        Assert.Contains("static bool global::System.IParsable<UserId>.TryParse(", output);
        Assert.Contains("static UserId global::System.ISpanParsable<UserId>.Parse(", output);
        Assert.Contains("static bool global::System.ISpanParsable<UserId>.TryParse(", output);
    }
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests --filter ParsableTests`
Expected: failures.

- [ ] **Step 3: Create `Emit/ParsableEmitter.cs`**

```csharp
using System.Text;
using Moongazing.OrionKey.Generators.Model;

namespace Moongazing.OrionKey.Generators.Emit;

/// <summary>Emits explicit IParsable/ISpanParsable members for minimal-API binding.</summary>
internal static class ParsableEmitter
{
    public static string Emit(OrionIdModel model)
    {
        var name = model.Name;

        // (parse-from-string, try-parse-from-string-body) per value type.
        // The body assigns `result` and returns bool.
        var (parseStr, tryBody) = model.ValueType switch
        {
            ValueType.Guid => (
                "new(global::System.Guid.Parse(s))",
                "if (global::System.Guid.TryParse(s, provider, out var v)) { result = new(v); return true; } result = default; return false;"),
            ValueType.Int32 => (
                "new(int.Parse(s, provider))",
                "if (int.TryParse(s, provider, out var v)) { result = new(v); return true; } result = default; return false;"),
            ValueType.Int64 => (
                "new(long.Parse(s, provider))",
                "if (long.TryParse(s, provider, out var v)) { result = new(v); return true; } result = default; return false;"),
            ValueType.String => (
                "new(s.ToString())",
                "if (s is null) { result = default; return false; } result = new(s.ToString()); return true;"),
            _ => throw new System.ArgumentOutOfRangeException(nameof(model)),
        };

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        if (model.Namespace.Length != 0)
        {
            sb.AppendLine($"namespace {model.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"readonly partial struct {name} : "
                    + $"global::System.IParsable<{name}>, global::System.ISpanParsable<{name}>");
        sb.AppendLine("{");

        // IParsable
        sb.AppendLine($"    static {name} global::System.IParsable<{name}>.Parse("
                    + "string s, global::System.IFormatProvider? provider) "
                    + $"=> {parseStr};");
        sb.AppendLine($"    static bool global::System.IParsable<{name}>.TryParse("
                    + "string? s, global::System.IFormatProvider? provider, "
                    + $"out {name} result) {{ {tryBody} }}");

        // ISpanParsable — delegate to the string overloads via ToString-on-span.
        sb.AppendLine($"    static {name} global::System.ISpanParsable<{name}>.Parse("
                    + "global::System.ReadOnlySpan<char> s, global::System.IFormatProvider? provider) "
                    + $"=> {ParseSpan(model)};");
        sb.AppendLine($"    static bool global::System.ISpanParsable<{name}>.TryParse("
                    + "global::System.ReadOnlySpan<char> s, global::System.IFormatProvider? provider, "
                    + $"out {name} result) {{ {TrySpanBody(model)} }}");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string ParseSpan(OrionIdModel model) => model.ValueType switch
    {
        ValueType.Guid => "new(global::System.Guid.Parse(s))",
        ValueType.Int32 => "new(int.Parse(s, provider))",
        ValueType.Int64 => "new(long.Parse(s, provider))",
        ValueType.String => "new(s.ToString())",
        _ => throw new System.ArgumentOutOfRangeException(nameof(model)),
    };

    private static string TrySpanBody(OrionIdModel model) => model.ValueType switch
    {
        ValueType.Guid =>
            "if (global::System.Guid.TryParse(s, provider, out var v)) { result = new(v); return true; } result = default; return false;",
        ValueType.Int32 =>
            "if (int.TryParse(s, provider, out var v)) { result = new(v); return true; } result = default; return false;",
        ValueType.Int64 =>
            "if (long.TryParse(s, provider, out var v)) { result = new(v); return true; } result = default; return false;",
        ValueType.String =>
            "result = new(s.ToString()); return true;",
        _ => throw new System.ArgumentOutOfRangeException(nameof(model)),
    };
}
```

- [ ] **Step 4: Wire it in**

```csharp
        spc.AddSource($"{model.Name}.OrionId.Parsable.g.cs", ParsableEmitter.Emit(model));
```

- [ ] **Step 5: Run tests, expect PASS**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests`
Expected: all pass.

- [ ] **Step 6: Commit**

```
git add src/Moongazing.OrionKey.Generators tests/Moongazing.OrionKey.Generators.Tests/ParsableTests.cs
git commit -m "feat(orionkey): IParsable/ISpanParsable emitter"
```

---

## Task 16: EF Core `ValueConverter` emitter (conditional)

**Files:**
- Create: `src/Moongazing.OrionKey.Generators/Emit/EfCoreConverterEmitter.cs`
- Modify: `src/Moongazing.OrionKey.Generators/OrionIdGenerator.cs`
- Test: `tests/Moongazing.OrionKey.Generators.Tests/EfCoreConverterTests.cs`

> **Conditional emission.** The EF Core converter is emitted **only** when the compilation references `Microsoft.EntityFrameworkCore`. Detect this in the generator by combining the per-struct provider with `context.CompilationProvider` and checking whether the type `Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter`2` is resolvable. The test harness includes EF Core assemblies automatically (it loads every loaded assembly), so the converter WILL emit in `.Generators.Tests` once EF Core is referenced by that test project — add `<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />` to `Moongazing.OrionKey.Generators.Tests.csproj`.
>
> **Target output.** A `file sealed class NameValueConverter : ValueConverter<Name, TValue>` with the convert-to-provider lambda `id => id.Value` and convert-from-provider lambda `value => new Name(value)`.

- [ ] **Step 1: Add EF Core to the generator test project**

In `tests/Moongazing.OrionKey.Generators.Tests/Moongazing.OrionKey.Generators.Tests.csproj`, add:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
```

- [ ] **Step 2: Write the failing tests**

```csharp
namespace Moongazing.OrionKey.Generators.Tests;

public class EfCoreConverterTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_ValueConverter_WhenEfCoreReferenced()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("class OrderIdValueConverter", output);
        Assert.Contains("ValueConverter<OrderId, global::System.Guid>", output);
    }

    [Fact]
    public void ValueConverter_RoundTripsThroughValue()
    {
        var output = Generate("OrionId<long, Snowflake>", "UserId");
        Assert.Contains("id => id.Value", output);
        Assert.Contains("value => new UserId(value)", output);
    }
}
```

> The "EF Core not referenced -> no converter" path is exercised in Task 19's integration tests where a compilation without EF Core is used; verifying it here is impractical because the harness always loads EF Core once the test project references it. The generator logic must still implement the conditional — see Step 4.

- [ ] **Step 3: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests --filter EfCoreConverterTests`
Expected: failures.

- [ ] **Step 4: Create `Emit/EfCoreConverterEmitter.cs`**

```csharp
using System.Text;
using Moongazing.OrionKey.Generators.Model;

namespace Moongazing.OrionKey.Generators.Emit;

/// <summary>Emits an EF Core ValueConverter. Only invoked when EF Core is referenced.</summary>
internal static class EfCoreConverterEmitter
{
    public static string Emit(OrionIdModel model)
    {
        var name = model.Name;
        var value = model.ValueKeyword;
        var converter = $"{name}ValueConverter";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        if (model.Namespace.Length != 0)
        {
            sb.AppendLine($"namespace {model.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"/// <summary>EF Core value converter for <see cref=\"{name}\"/>.</summary>");
        sb.AppendLine($"public sealed class {converter} : "
                    + "global::Microsoft.EntityFrameworkCore.Storage.ValueConversion."
                    + $"ValueConverter<{name}, {value}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public {converter}() : base(id => id.Value, value => new {name}(value)) {{ }}");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
```

- [ ] **Step 5: Wire the conditional into the generator**

The generator's `Handle` currently takes only a symbol. Change the pipeline so it also receives a flag "is EF Core referenced". In `Initialize`:

```csharp
        var efCoreReferenced = context.CompilationProvider.Select(static (compilation, _) =>
            compilation.GetTypeByMetadataName(
                "Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter`2") is not null);

        context.RegisterSourceOutput(oneArg.Combine(efCoreReferenced),
            static (spc, pair) => Handle(spc, pair.Left, pair.Right));
        context.RegisterSourceOutput(twoArg.Combine(efCoreReferenced),
            static (spc, pair) => Handle(spc, pair.Left, pair.Right));
```

Change `Handle`'s signature to `Handle(SourceProductionContext spc, INamedTypeSymbol? symbol, bool efCoreReferenced)` and, after the other emissions:

```csharp
        if (efCoreReferenced)
        {
            spc.AddSource($"{model.Name}.OrionId.EfCore.g.cs", EfCoreConverterEmitter.Emit(model));
        }
```

- [ ] **Step 6: Run tests, expect PASS**

Run: `dotnet test tests/Moongazing.OrionKey.Generators.Tests`
Expected: all pass.

- [ ] **Step 7: Commit**

```
git add src/Moongazing.OrionKey.Generators tests/Moongazing.OrionKey.Generators.Tests
git commit -m "feat(orionkey): conditional EF Core ValueConverter emitter"
```

---

## Task 17: Verify the generator packs into the `OrionKey` package

**Files:**
- Possibly modify: `src/Moongazing.OrionKey/Moongazing.OrionKey.csproj`

- [ ] **Step 1: Pack the `OrionKey` package**

Run: `dotnet pack src/Moongazing.OrionKey -c Release -o ./artifacts`
Expected: `./artifacts/OrionKey.0.1.0.nupkg` is produced.

- [ ] **Step 2: Inspect the package contents**

Run: `unzip -l artifacts/OrionKey.0.1.0.nupkg`
Expected entries:
- `lib/net8.0/Moongazing.OrionKey.dll`, `lib/net9.0/...`, `lib/net10.0/...`
- `analyzers/dotnet/cs/Moongazing.OrionKey.Generators.dll`

If the `analyzers/dotnet/cs` entry is missing, the `None Include` path in Task 1 Step 2 did not resolve. Fix by replacing the `None Include` with a target that runs after build:

```xml
<Target Name="PackGenerator" BeforeTargets="GenerateNuspec">
  <ItemGroup>
    <_PackageFiles Include="$(MSBuildThisFileDirectory)..\Moongazing.OrionKey.Generators\bin\$(Configuration)\netstandard2.0\Moongazing.OrionKey.Generators.dll">
      <PackagePath>analyzers/dotnet/cs</PackagePath>
      <BuildAction>None</BuildAction>
    </_PackageFiles>
  </ItemGroup>
</Target>
```

Re-pack and re-inspect until the analyzer is present.

- [ ] **Step 3: Smoke-test the package against the sample project**

Temporarily point `sample/Moongazing.OrionKey.Sample` at the packed nupkg (add `./artifacts` as a local NuGet source, reference `OrionKey 0.1.0`), declare one `[OrionId<Guid>]` struct, build the sample. Expected: the generated members are available (the sample uses `OrderId.New()` and compiles). Revert the sample back to the `ProjectReference` afterwards — the local-package test is a one-off verification, not the committed state.

- [ ] **Step 4: Commit (only if csproj changed)**

```
git add src/Moongazing.OrionKey/Moongazing.OrionKey.csproj
git commit -m "build(orionkey): pack the source generator into the OrionKey analyzer folder"
```

If no csproj change was needed, skip the commit and note "Task 17: verified, no change required."

---

## Task 18: `OrionKey.Testing` — deterministic generators

**Files:**
- Create: `src/Moongazing.OrionKey.Testing/SequentialGenerators.cs`
- Create: `src/Moongazing.OrionKey.Testing/DeterministicIdScope.cs`
- Delete: `src/Moongazing.OrionKey.Testing/_Placeholder.cs`
- Test: `tests/Moongazing.OrionKey.Testing.Tests/SequentialGeneratorsTests.cs`
- Test: `tests/Moongazing.OrionKey.Testing.Tests/DeterministicIdScopeTests.cs`

> **Context.** Production `OrionKey` exposes an `internal static void ResetForTesting()` (added in Task 7) and `Moongazing.OrionKey.Testing` is in the `InternalsVisibleTo` list. `DeterministicIdScope` uses that reset hook to give tests a clean, predictable generator and restores process state on dispose.

- [ ] **Step 1: Write the failing tests**

`tests/Moongazing.OrionKey.Testing.Tests/SequentialGeneratorsTests.cs`:

```csharp
using Moongazing.OrionKey.Testing;

namespace Moongazing.OrionKey.Testing.Tests;

public class SequentialGeneratorsTests
{
    [Fact]
    public void SequentialSnowflake_ShouldProduceAscendingValuesFromOne()
    {
        var gen = new SequentialSnowflake();
        Assert.Equal(1, gen.Next());
        Assert.Equal(2, gen.Next());
        Assert.Equal(3, gen.Next());
    }

    [Fact]
    public void SequentialUlid_ShouldProduce26CharAscendingValues()
    {
        var gen = new SequentialUlid();
        var first = gen.Next();
        var second = gen.Next();
        Assert.Equal(26, first.Length);
        Assert.True(string.CompareOrdinal(first, second) < 0);
    }

    [Fact]
    public void SequentialNanoId_ShouldProduce21CharDistinctValues()
    {
        var gen = new SequentialNanoId();
        Assert.NotEqual(gen.Next(), gen.Next());
        Assert.Equal(21, new SequentialNanoId().Next().Length);
    }
}
```

`tests/Moongazing.OrionKey.Testing.Tests/DeterministicIdScopeTests.cs`:

```csharp
using Moongazing.OrionKey.Testing;

namespace Moongazing.OrionKey.Testing.Tests;

[CollectionDefinition("OrionKeyProcessState", DisableParallelization = true)]
public sealed class OrionKeyProcessStateCollection;

[Collection("OrionKeyProcessState")]
public class DeterministicIdScopeTests
{
    [Fact]
    public void Scope_ShouldMakeSnowflakeDeterministic()
    {
        using var scope = new DeterministicIdScope();
        Assert.Equal(1, OrionKey.NextSnowflake());
        Assert.Equal(2, OrionKey.NextSnowflake());
    }

    [Fact]
    public void Scope_ShouldRestoreState_OnDispose()
    {
        long insideScope;
        using (new DeterministicIdScope())
        {
            insideScope = OrionKey.NextSnowflake();
        }
        var afterScope = OrionKey.NextSnowflake();
        Assert.Equal(1, insideScope);
        // After dispose the real generator is back: a real Snowflake id is far larger than 2.
        Assert.True(afterScope > 1_000);
    }
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/Moongazing.OrionKey.Testing.Tests`
Expected: build error.

- [ ] **Step 3: Create `SequentialGenerators.cs`**

```csharp
namespace Moongazing.OrionKey.Testing;

/// <summary>Produces Snowflake-shaped <see cref="long"/> ids as 1, 2, 3, ... for deterministic tests.</summary>
public sealed class SequentialSnowflake
{
    private long current;

    /// <summary>Returns the next sequential id.</summary>
    public long Next() => Interlocked.Increment(ref current);
}

/// <summary>Produces ascending 26-character ULID-shaped strings for deterministic tests.</summary>
public sealed class SequentialUlid
{
    private long current;

    /// <summary>Returns the next sequential ULID-shaped string.</summary>
    public string Next()
    {
        var n = Interlocked.Increment(ref current);
        // Zero-padded uppercase so ordinal sort equals numeric order.
        return n.ToString("D26", System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>Produces distinct 21-character NanoId-shaped strings for deterministic tests.</summary>
public sealed class SequentialNanoId
{
    private long current;

    /// <summary>Returns the next sequential NanoId-shaped string.</summary>
    public string Next()
    {
        var n = Interlocked.Increment(ref current);
        return n.ToString("D21", System.Globalization.CultureInfo.InvariantCulture);
    }
}
```

- [ ] **Step 4: Create `DeterministicIdScope.cs`**

```csharp
namespace Moongazing.OrionKey.Testing;

/// <summary>
/// Swaps OrionKey's process-wide generators for deterministic sequences for the lifetime of the
/// scope, then restores normal behaviour on <see cref="Dispose"/>. Tests that use this type must
/// not run in parallel with each other or with code that generates ids.
/// </summary>
public sealed class DeterministicIdScope : IDisposable
{
    /// <summary>Begins a deterministic-id scope.</summary>
    public DeterministicIdScope()
    {
        OrionKey.ResetForTesting();
        OrionKey.UseDeterministicGeneratorsForTesting();
    }

    /// <summary>Restores OrionKey to its default (non-deterministic) generators.</summary>
    public void Dispose() => OrionKey.ResetForTesting();
}
```

This requires a small addition to the production `OrionKey` facade. In `src/Moongazing.OrionKey/OrionKey.cs` add an `internal` hook used only by `OrionKey.Testing`:

```csharp
    private static SequentialState? deterministic;

    /// <summary>Switches to deterministic 1,2,3,... generators. For OrionKey.Testing only.</summary>
    internal static void UseDeterministicGeneratorsForTesting()
    {
        lock (Gate)
        {
            deterministic = new SequentialState();
        }
    }

    private sealed class SequentialState
    {
        public long Snowflake;
    }
```

And make `NextSnowflake` honour it — change `NextSnowflake` to:

```csharp
    public static long NextSnowflake()
    {
        lock (Gate)
        {
            if (deterministic is not null)
            {
                return ++deterministic.Snowflake;
            }
        }
        var generator = GetSnowflake();
        var id = generator.Next();
        OrionKeyDiagnostics.RecordGenerated("snowflake", options.EnableMetrics);
        return id;
    }
```

Also extend `ResetForTesting` to clear `deterministic = null;`.

> Keep this minimal: only `NextSnowflake` needs the deterministic path for the spec'd tests. If `NewUlid`/`NewNanoId` deterministic paths are wanted later they follow the same shape; the spec's §9 sequential generators (`SequentialUlid` etc.) are also usable directly without the process swap.

- [ ] **Step 5: Delete the placeholder, run tests, expect PASS**

Delete `src/Moongazing.OrionKey.Testing/_Placeholder.cs`.
Run: `dotnet test tests/Moongazing.OrionKey.Testing.Tests`
Expected: 5 tests pass.

- [ ] **Step 6: Commit**

```
git add src/Moongazing.OrionKey src/Moongazing.OrionKey.Testing tests/Moongazing.OrionKey.Testing.Tests
git commit -m "feat(orionkey): OrionKey.Testing deterministic generators and id scope"
```

---

## Task 19: Integration tests — JSON, EF Core, minimal API, MVC round-trips

**Files:**
- Modify: `tests/Moongazing.OrionKey.IntegrationTests/Moongazing.OrionKey.IntegrationTests.csproj`
- Create: `tests/Moongazing.OrionKey.IntegrationTests/Ids.cs`
- Create: `tests/Moongazing.OrionKey.IntegrationTests/JsonRoundTripTests.cs`
- Create: `tests/Moongazing.OrionKey.IntegrationTests/EfCoreRoundTripTests.cs`
- Create: `tests/Moongazing.OrionKey.IntegrationTests/MinimalApiBindingTests.cs`

- [ ] **Step 1: Add the integration packages**

In `Moongazing.OrionKey.IntegrationTests.csproj` add:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
```

and `<FrameworkReference Include="Microsoft.AspNetCore.App" />`.

- [ ] **Step 2: Declare the IDs under test**

`tests/Moongazing.OrionKey.IntegrationTests/Ids.cs`:

```csharp
using Moongazing.OrionKey;

namespace Moongazing.OrionKey.IntegrationTests;

[OrionId<System.Guid>]            public readonly partial struct OrderId;
[OrionId<long, Snowflake>]        public readonly partial struct UserId;
[OrionId<string, Ulid>]           public readonly partial struct TenantId;
```

- [ ] **Step 3: JSON round-trip test**

`JsonRoundTripTests.cs`:

```csharp
using System.Text.Json;

namespace Moongazing.OrionKey.IntegrationTests;

public class JsonRoundTripTests
{
    [Fact]
    public void OrderId_ShouldRoundTripThroughJson()
    {
        var original = OrderId.New();
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<OrderId>(json);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void UserId_ShouldSerializeAsRawNumber()
    {
        var id = new UserId(123456789);
        Assert.Equal("123456789", JsonSerializer.Serialize(id));
    }

    [Fact]
    public void TenantId_ShouldSerializeAsRawString()
    {
        var id = new TenantId("01HZY0000000000000000000AB");
        Assert.Equal("\"01HZY0000000000000000000AB\"", JsonSerializer.Serialize(id));
    }
}
```

- [ ] **Step 4: EF Core round-trip test**

`EfCoreRoundTripTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Moongazing.OrionKey.IntegrationTests;

public class EfCoreRoundTripTests
{
    private sealed class Order
    {
        public OrderId Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>().HasKey(o => o.Id);
            modelBuilder.Entity<Order>().Property(o => o.Id)
                .HasConversion(new OrderIdValueConverter());
        }
    }

    [Fact]
    public async Task Order_ShouldPersistAndReload_WithGeneratedValueConverter()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options;

        var id = OrderId.New();
        await using (var ctx = new TestDbContext(options))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Orders.Add(new Order { Id = id, Name = "Widget" });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TestDbContext(options))
        {
            var reloaded = await ctx.Orders.SingleAsync();
            Assert.Equal(id, reloaded.Id);
            Assert.Equal("Widget", reloaded.Name);
        }
    }
}
```

- [ ] **Step 5: Minimal API binding test**

`MinimalApiBindingTests.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Moongazing.OrionKey.IntegrationTests;

public class MinimalApiBindingTests : IClassFixture<WebApplicationFactory<MinimalApiBindingTests.Marker>>
{
    public sealed class Marker;

    private readonly WebApplicationFactory<Marker> factory;

    public MinimalApiBindingTests(WebApplicationFactory<Marker> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
            builder.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                    endpoints.MapGet("/orders/{id}", (OrderId id) => Results.Ok(id.ToString())));
            }));
    }

    [Fact]
    public async Task Route_ShouldBindOrderId_ViaIParsable()
    {
        var id = OrderId.New();
        var client = factory.CreateClient();
        var response = await client.GetAsync($"/orders/{id}");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(id.ToString(), body);
    }
}
```

> If `WebApplicationFactory` with the bare `Marker` class needs an entry point, add a minimal `Program` partial or switch to `WebApplication.CreateBuilder` hosted in-test. Use whichever the installed ASP.NET Core test packages support cleanly; the assertion (route binds `OrderId` through the generated `IParsable`) is what matters.

- [ ] **Step 6: Run, expect PASS**

Run: `dotnet test tests/Moongazing.OrionKey.IntegrationTests`
Expected: all integration tests pass. The generated `OrderIdValueConverter` exists because the IntegrationTests project references EF Core (Step 1) — proving the conditional emission fires.

- [ ] **Step 7: Commit**

```
git add tests/Moongazing.OrionKey.IntegrationTests
git commit -m "test(orionkey): integration tests for JSON, EF Core, and minimal-API binding"
```

---

## Task 20: Benchmarks

**Files:**
- Create: `bench/Moongazing.OrionKey.Benchmarks/Program.cs`
- Create: `bench/Moongazing.OrionKey.Benchmarks/IdGenerationBenchmarks.cs`

- [ ] **Step 1: Create `IdGenerationBenchmarks.cs`**

```csharp
using BenchmarkDotNet.Attributes;
using Moongazing.OrionKey;

namespace Moongazing.OrionKey.Benchmarks;

[MemoryDiagnoser]
public class IdGenerationBenchmarks
{
    [Benchmark(Baseline = true)]
    public Guid RawGuid() => Guid.NewGuid();

    [Benchmark]
    public long Snowflake() => OrionKey.NextSnowflake();

    [Benchmark]
    public string Ulid() => OrionKey.NewUlid();

    [Benchmark]
    public string NanoId() => OrionKey.NewNanoId();

    [Benchmark]
    public Guid GuidV7() => OrionKey.NewGuidV7();
}
```

- [ ] **Step 2: Create `Program.cs`**

```csharp
using BenchmarkDotNet.Running;
using Moongazing.OrionKey.Benchmarks;

BenchmarkRunner.Run<IdGenerationBenchmarks>();
```

- [ ] **Step 3: Build (do not run the full benchmark in CI)**

Run: `dotnet build bench/Moongazing.OrionKey.Benchmarks -c Release`
Expected: success.

- [ ] **Step 4: Commit**

```
git add bench/Moongazing.OrionKey.Benchmarks
git commit -m "bench(orionkey): id-generation throughput benchmarks"
```

---

## Task 21: Sample application

**Files:**
- Create: `sample/Moongazing.OrionKey.Sample/Program.cs`
- Create: `sample/Moongazing.OrionKey.Sample/Ids.cs`

- [ ] **Step 1: Create `Ids.cs`**

```csharp
using Moongazing.OrionKey;

namespace Moongazing.OrionKey.Sample;

[OrionId<System.Guid>]       public readonly partial struct OrderId;
[OrionId<long, Snowflake>]   public readonly partial struct UserId;
[OrionId<string, Ulid>]      public readonly partial struct TenantId;
[OrionId<string, NanoId>]    public readonly partial struct SessionId;
```

- [ ] **Step 2: Create `Program.cs`**

```csharp
using Moongazing.OrionKey;
using Moongazing.OrionKey.Sample;

OrionKey.Configure(o => o.SnowflakeWorkerId = 1);

var order = OrderId.New();
var user = UserId.New();
var tenant = TenantId.New();
var session = SessionId.New();

Console.WriteLine($"OrderId   (Guid)      : {order}");
Console.WriteLine($"UserId    (Snowflake) : {user}");
Console.WriteLine($"TenantId  (ULID)      : {tenant}");
Console.WriteLine($"SessionId (NanoId)    : {session}");

// Equality and parsing.
var parsed = System.Guid.TryParse(order.ToString(), out var g) ? new OrderId(g) : OrderId.Empty;
Console.WriteLine($"Round-trip equal      : {order == parsed}");

// Comparison (UserId is sortable).
var earlier = UserId.New();
var later = UserId.New();
Console.WriteLine($"Snowflake ordered     : {earlier < later}");
```

- [ ] **Step 3: Build and run**

Run: `dotnet run --project sample/Moongazing.OrionKey.Sample -c Release`
Expected: prints four ids and two `True` lines.

- [ ] **Step 4: Commit**

```
git add sample/Moongazing.OrionKey.Sample
git commit -m "sample(orionkey): end-to-end sample exercising every strategy"
```

---

## Task 22: Documentation — README, CHANGELOG, docs

**Files:**
- Create: `README.md`
- Create: `CHANGELOG.md`
- Create: `LICENSE.txt`
- Create: `docs/snowflake-worker-ids.md`

- [ ] **Step 1: Create `LICENSE.txt`**

Standard MIT license text, copyright `2026 Tunahan Ali Ozturk`.

- [ ] **Step 2: Create `README.md`**

Sections, in order:
- Title `OrionKey`, one-line pitch: "Source-generated strongly-typed IDs for .NET."
- NuGet/license/target badges (mirror the OrionGuard README badge block, package id `OrionKey`).
- **Quick start**: `dotnet add package OrionKey`, then the four-line attribute example from §1 of the spec.
- **The attribute table**: reproduce the §4.1 combination table from the spec.
- **What gets generated**: the six emitted companions list from spec §5.
- **Snowflake worker IDs**: short paragraph + link to `docs/snowflake-worker-ids.md`.
- **Testing**: `OrionKey.Testing`, `DeterministicIdScope`.
- **More from the Orion family**: bullet list — OrionGuard, OrionAudit (link each to its GitHub repo).
- No emojis, no buzzwords.

- [ ] **Step 3: Create `CHANGELOG.md`**

```markdown
# Changelog

All notable changes to OrionKey are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-05-20

### Added

- `[OrionId<TValue>]` and `[OrionId<TValue, TStrategy>]` attributes turning a `readonly partial struct` into a strongly-typed id.
- Strategies: `Guid`, `GuidV7`, `Snowflake` (long), `Ulid` (string), `NanoId` (string); `int`/`long` externally-assigned ids.
- Bundled Roslyn incremental generator emitting: struct body with `New()`, `IEquatable`, `IComparable` (sortable strategies), `System.Text.Json` converter, `TypeConverter`, `IParsable`/`ISpanParsable`, and a conditional EF Core `ValueConverter`.
- Runtime ID generators: `SnowflakeIdGenerator`, `UlidFactory`, `NanoIdFactory`, `GuidV7Factory`.
- `OrionKey.Configure` for Snowflake worker-id and epoch; environment-variable and machine-name fallback.
- Diagnostics `ORIONKEY001`-`ORIONKEY005`.
- `OrionKey.Testing` package with `DeterministicIdScope` and sequential generators.
```

- [ ] **Step 4: Create `docs/snowflake-worker-ids.md`**

Explain: the 10-bit worker id, why it must be unique per instance, the three resolution sources (`OrionKey.Configure`, `ORIONKEY_WORKER_ID`, machine-name hash), the one-time auto-derivation warning, and a Kubernetes example pinning `ORIONKEY_WORKER_ID` from the pod ordinal.

- [ ] **Step 5: Commit**

```
git add README.md CHANGELOG.md LICENSE.txt docs/snowflake-worker-ids.md
git commit -m "docs(orionkey): README, CHANGELOG, license, Snowflake worker-id guide"
```

---

## Task 23: Package metadata polish and final verification

**Files:**
- Modify: `src/Moongazing.OrionKey/Moongazing.OrionKey.csproj`
- Modify: `src/Moongazing.OrionKey.Testing/Moongazing.OrionKey.Testing.csproj`
- Create: `src/Moongazing.OrionKey/docs/README.md` and `src/Moongazing.OrionKey.Testing/docs/README.md`

- [ ] **Step 1: Add per-package READMEs and packaging metadata**

Create a short `docs/README.md` inside each packable project (a trimmed version of the root README focused on that package). Add to each packable csproj:

```xml
<PackageReadmeFile>docs/README.md</PackageReadmeFile>
```

and the pack item:

```xml
<None Include="docs/README.md" Pack="true" PackagePath="docs/" />
```

If a logo file `docs/logo.png` is available, also add `<PackageIcon>docs/logo.png</PackageIcon>` and pack it. If no logo exists yet, omit `PackageIcon` (do not block the task on artwork).

- [ ] **Step 2: Full solution build**

Run: `dotnet build -c Release`
Expected: success, zero warnings across all TFMs.

- [ ] **Step 3: Full test run**

Run: `dotnet test -c Release`
Expected: every test project green — `Tests`, `Generators.Tests`, `IntegrationTests`, `Testing.Tests`.

- [ ] **Step 4: Pack both packages**

Run: `dotnet pack -c Release -o ./artifacts`
Expected: `artifacts/OrionKey.0.1.0.nupkg` and `artifacts/OrionKey.Testing.0.1.0.nupkg`. Confirm `OrionKey.0.1.0.nupkg` still contains `analyzers/dotnet/cs/Moongazing.OrionKey.Generators.dll` (re-run the Task 17 `unzip -l` check).

- [ ] **Step 5: Commit**

```
git add src/Moongazing.OrionKey src/Moongazing.OrionKey.Testing
git commit -m "build(orionkey): per-package READMEs and packaging metadata"
```

---

## Final verification

- [ ] `dotnet build -c Release` — clean, zero warnings.
- [ ] `dotnet test -c Release` — all four test projects green.
- [ ] `dotnet pack -c Release -o ./artifacts` — two `.nupkg` files; `OrionKey` contains the analyzer.
- [ ] `dotnet run --project sample/Moongazing.OrionKey.Sample` — prints ids and `True` lines.
- [ ] `git log --oneline` — one commit per task, in order.

---

## Self-Review

**Spec coverage:**

| Spec section | Task(s) |
|---|---|
| §3 solution/package layout | Task 1 |
| §3.1 single-package runtime+analyzer | Task 1, Task 17 |
| §4 `[OrionId]` attribute + combinations | Task 7 (attribute), Task 9 (parsing) |
| §4.2 strategy markers | Task 7 |
| §4.3 diagnostics ORIONKEY001-005 | Task 9 (descriptors), Task 10 (wiring) |
| §5.1 core body + `New()` | Task 11 |
| §5.2 `IComparable` | Task 12 |
| §5.3 EF Core `ValueConverter` (conditional) | Task 16 |
| §5.4 `JsonConverter` | Task 13 |
| §5.5 `TypeConverter` | Task 14 |
| §5.6 `IParsable`/`ISpanParsable` | Task 15 |
| §6 Snowflake configuration | Task 2 (resolver/options), Task 7 (facade) |
| §7 runtime types | Tasks 2-7 |
| §7.1 no third-party dependency | Tasks 3-6 (in-package algorithms) |
| §8 diagnostics/OpenTelemetry | Task 2 (`OrionKeyDiagnostics`) |
| §9 `OrionKey.Testing` | Task 18 |
| §10 versioning / `Directory.Build.props` | Task 1 |
| §11 testing strategy | Tasks 2-6, 8-16 (unit), 19 (integration), 20 (bench) |
| §12 documentation | Task 22, Task 23 |
| §13 OrionGuard downstream | out of scope — not a task, correctly excluded |

Every in-scope spec section maps to at least one task. §13 is explicitly downstream and has no task, as intended.

**Type consistency:** The generator-side `OrionKeyDiagnostics` (descriptors, `Moongazing.OrionKey.Generators.Diagnostics`) and the runtime-side `OrionKeyDiagnostics` (counter/warning, `Moongazing.OrionKey.Diagnostics`) are deliberately distinct types in distinct namespaces/assemblies — not a naming bug, but implementers must not merge them. `OrionIdModel.GeneratesNew`, `IsSortable`, `ValueKeyword` are defined in Task 9 and used consistently in Tasks 11-16. `OrionKey.NextSnowflake/NewUlid/NewNanoId/NewGuidV7` are defined in Task 7 and referenced by the emitters in Task 11.

**Placeholder scan:** No `TBD`/`TODO`. The `_Placeholder.cs` files are a real, intentional scaffolding step (Task 1 Step 7) deleted in Tasks 7/8/18; not plan placeholders.
