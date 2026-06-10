namespace Moongazing.OrionKey.Generators.Tests;

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Moongazing.OrionKey.Generators;
using Xunit;

public sealed class IncrementalCacheTests
{
    private const string AttributeSource = """
        namespace Moongazing.OrionKey;
        using System;
        [AttributeUsage(AttributeTargets.Struct)]
        public sealed class OrionIdAttribute<T> : Attribute { }
        [AttributeUsage(AttributeTargets.Struct)]
        public sealed class OrionIdAttribute<TValue, TStrategy> : Attribute { }
        public sealed class Snowflake { }
        public sealed class Ulid { }
        public sealed class NanoId { }
        public sealed class GuidV7 { }
        public sealed class Cuid2 { }
        public sealed class Ksuid { }
        public sealed class ObjectId { }
        public sealed class SequentialGuid { }
        """;

    private const string OrionIdSource = """
        namespace Demo;

        [Moongazing.OrionKey.OrionId<long>]
        public readonly partial struct UserId { }
        """;

    private static CSharpCompilation BuildCompilation(params string[] sources)
    {
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();
        var refs = System.AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location) as MetadataReference)
            .ToList();
        return CSharpCompilation.Create("CacheTest", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpGeneratorDriver NewDriver()
    {
        var generator = new OrionIdGenerator();
        return CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            additionalTexts: System.Collections.Immutable.ImmutableArray<AdditionalText>.Empty,
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
    }

    [Fact]
    public void Identical_compilation_reuses_cached_outputs()
    {
        var compilation = BuildCompilation(AttributeSource, OrionIdSource);
        var driver = (CSharpGeneratorDriver)NewDriver().RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        // Re-run with the SAME compilation - every tracked OUTPUT step must report Cached.
        var rerun = driver.RunGenerators(compilation);

        var result = rerun.GetRunResult().Results.Single();
        AssertAllTrackedStepsCached(result, requireAtLeastOne: true);
    }

    [Fact]
    public void Unrelated_source_edit_does_NOT_re_run_OrionId_transform()
    {
        var compilation = BuildCompilation(AttributeSource, OrionIdSource);
        var driver = (CSharpGeneratorDriver)NewDriver().RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        // Add an unrelated source tree (no OrionId attribute, no struct decl).
        var unrelated = CSharpSyntaxTree.ParseText("namespace Demo { public class Junk { public int X { get; set; } } }");
        var nextCompilation = compilation.AddSyntaxTrees(unrelated);

        var rerun = driver.RunGenerators(nextCompilation);

        var result = rerun.GetRunResult().Results.Single();
        AssertAllTrackedStepsCached(result, requireAtLeastOne: true);
    }

    private static void AssertAllTrackedStepsCached(GeneratorRunResult result, bool requireAtLeastOne)
    {
        // Assert on the OrionId-specific stages we tagged with WithTrackingName. Built-in
        // 'Compilation' / 'SyntaxTrees' may legitimately rerun when a new tree is added.
        var trackedNames = new[] { "OrionId_OneArg_Parse", "OrionId_TwoArg_Parse", "OrionId_AllModels" };
        var observedSteps = 0;
        foreach (var name in trackedNames)
        {
            if (!result.TrackedSteps.TryGetValue(name, out var steps))
            {
                continue;
            }
            observedSteps++;
            foreach (var step in steps)
            {
                foreach (var output in step.Outputs)
                {
                    Assert.True(
                        output.Reason is IncrementalStepRunReason.Cached
                            or IncrementalStepRunReason.Unchanged,
                        $"Step '{name}' output reason {output.Reason} - cache regression");
                }
            }
        }
        if (requireAtLeastOne)
        {
            Assert.True(observedSteps > 0,
                "No OrionId tracked steps observed - generator pipeline shape changed.");
        }
    }
}
