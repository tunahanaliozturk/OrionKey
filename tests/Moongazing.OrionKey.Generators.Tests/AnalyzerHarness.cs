using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Moongazing.OrionKey.Generators.Tests;

/// <summary>
/// Runs a DiagnosticAnalyzer against one or more source strings and returns the analyzer
/// diagnostics. Mirrors GeneratorHarness's reference set so ORIONKEY006/007 see the same
/// EF Core / Newtonsoft.Json / etc. assemblies the runtime harness does.
/// </summary>
internal static class AnalyzerHarness
{
    public static Task<ImmutableArray<Diagnostic>> RunAsync(DiagnosticAnalyzer analyzer, params string[] sources)
    {
        // Force the runtime assemblies to load - same dance as GeneratorHarness.
        _ = typeof(global::Moongazing.OrionKey.OrionIdAttribute<>).Assembly;
        _ = typeof(global::Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<,>).Assembly;
        _ = typeof(global::Microsoft.EntityFrameworkCore.DbContext).Assembly;
        _ = typeof(global::Dapper.SqlMapper).Assembly;
        _ = typeof(global::Newtonsoft.Json.JsonConverter).Assembly;
        _ = typeof(global::MongoDB.Bson.Serialization.Serializers.SerializerBase<>).Assembly;
        _ = typeof(global::Swashbuckle.AspNetCore.SwaggerGen.ISchemaFilter).Assembly;

        IEnumerable<System.Reflection.Assembly> assemblies = System.AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location));

        var references = assemblies
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();

        // Default path is "". Tests that need to simulate generated trees override the path
        // via RunWithGeneratedAsync below.

        var compilation = CSharpCompilation.Create(
            "AnalyzerTestAssembly",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var withAnalyzer = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));
        return withAnalyzer.GetAnalyzerDiagnosticsAsync();
    }

    /// <summary>
    /// Variant of <see cref="RunAsync(DiagnosticAnalyzer, string[])"/> that splits sources
    /// into user-authored and generator-emitted trees. Generated trees are parsed with a
    /// `.g.cs` path so analyzers using path-based generated-code detection (such as
    /// <c>UnusedOrionIdAnalyzer</c>) treat them as generator output. Used by ORIONKEY007
    /// tests to assert the analyzer ignores generator-emitted partials when counting
    /// references.
    /// </summary>
    public static Task<ImmutableArray<Diagnostic>> RunWithGeneratedAsync(
        DiagnosticAnalyzer analyzer,
        string[] userSources,
        string[] generatedSources)
    {
        _ = typeof(global::Moongazing.OrionKey.OrionIdAttribute<>).Assembly;
        _ = typeof(global::Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<,>).Assembly;
        _ = typeof(global::Microsoft.EntityFrameworkCore.DbContext).Assembly;
        _ = typeof(global::Dapper.SqlMapper).Assembly;
        _ = typeof(global::Newtonsoft.Json.JsonConverter).Assembly;
        _ = typeof(global::MongoDB.Bson.Serialization.Serializers.SerializerBase<>).Assembly;
        _ = typeof(global::Swashbuckle.AspNetCore.SwaggerGen.ISchemaFilter).Assembly;

        IEnumerable<System.Reflection.Assembly> assemblies = System.AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location));

        var references = assemblies
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var userTrees = userSources
            .Select((s, i) => CSharpSyntaxTree.ParseText(s, path: $"User_{i}.cs"));
        var generatedTrees = generatedSources
            .Select((s, i) => CSharpSyntaxTree.ParseText(s, path: $"OrionKey_{i}.g.cs"));
        var trees = userTrees.Concat(generatedTrees).ToArray();

        var compilation = CSharpCompilation.Create(
            "AnalyzerTestAssembly",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var withAnalyzer = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));
        return withAnalyzer.GetAnalyzerDiagnosticsAsync();
    }

    public static IEnumerable<string> Ids(this ImmutableArray<Diagnostic> diags)
        => diags.Select(d => d.Id);
}
