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

        // Force the runtime assembly to load so its metadata is available as a reference.
        // Without this, nothing statically binds an OrionKey type and the assembly is
        // absent from AppDomain.CurrentDomain.GetAssemblies().
        _ = typeof(global::Moongazing.OrionKey.OrionIdAttribute<>).Assembly;

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
