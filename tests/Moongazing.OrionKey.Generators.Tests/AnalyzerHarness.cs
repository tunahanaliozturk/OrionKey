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
