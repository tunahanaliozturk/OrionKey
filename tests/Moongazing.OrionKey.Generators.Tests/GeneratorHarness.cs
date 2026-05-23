using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Moongazing.OrionKey.Generators;

namespace Moongazing.OrionKey.Generators.Tests;

/// <summary>Runs OrionIdGenerator against a source string and exposes the result.</summary>
internal static class GeneratorHarness
{
    public static GeneratorRunResult Run(string source)
        => Run(source, excludeAssemblyNamePrefixes: System.Array.Empty<string>());

    /// <summary>
    /// Runs the generator with the given source while excluding any assembly whose simple
    /// name starts with one of the supplied prefixes. Use this for negative-emission tests
    /// that assert "without package X referenced, emitter Y produces nothing".
    /// </summary>
    public static GeneratorRunResult Run(string source, params string[] excludeAssemblyNamePrefixes)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Force the runtime assembly to load so its metadata is available as a reference.
        _ = typeof(global::Moongazing.OrionKey.OrionIdAttribute<>).Assembly;

        // Force the EF Core assembly to load so the conditional ValueConverter emitter
        // sees Microsoft.EntityFrameworkCore as a referenced assembly.
        _ = typeof(global::Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<,>).Assembly;

        IEnumerable<System.Reflection.Assembly> assemblies = System.AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location));

        if (excludeAssemblyNamePrefixes.Length > 0)
        {
            assemblies = assemblies.Where(a =>
            {
                var name = a.GetName().Name ?? string.Empty;
                return !excludeAssemblyNamePrefixes.Any(prefix =>
                    name.StartsWith(prefix, System.StringComparison.Ordinal));
            });
        }

        var references = assemblies
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
