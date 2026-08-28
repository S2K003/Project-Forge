using System.Collections.Immutable;
using ForgeOps.Contracts.Forge;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ForgeOps.Forge;

/// <summary>
/// Compiles generated source with Roslyn against a <b>curated</b> reference set — only the
/// base runtime assemblies plus a few safe libraries. Anything under IO/Net/interop simply
/// will not resolve, which is defence-in-depth behind <see cref="BannedApiScanner"/>.
/// </summary>
public sealed class RoslynCompiler
{
    private static readonly string[] AllowedReferenceSimpleNames =
    [
        "System.Private.CoreLib",
        "System.Runtime",
        "System.Runtime.Numerics",
        "System.Collections",
        "System.Collections.Immutable",
        "System.Linq",
        "System.Linq.Expressions",
        "System.Text.RegularExpressions",
        "System.ObjectModel",
        "System.ComponentModel",
        "System.ComponentModel.Primitives",
        "System.Console",
        "System.Memory",
        "System.Globalization",
        "netstandard",
    ];

    private readonly Lazy<ImmutableArray<MetadataReference>> _references = new(BuildReferences);

    public CompilationResult Compile(string assemblyName, IReadOnlyDictionary<string, string> sources)
    {
        var syntaxTrees = sources
            .Select(kvp => CSharpSyntaxTree.ParseText(
                SourceText.From(kvp.Value),
                new CSharpParseOptions(LanguageVersion.Latest),
                path: kvp.Key))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            _references.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: false,
                nullableContextOptions: NullableContextOptions.Enable));

        using var peStream = new MemoryStream();
        var emit = compilation.Emit(peStream);

        var diagnostics = emit.Diagnostics
            .Where(d => d.Severity >= Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .Select(Map)
            .OrderByDescending(d => d.Severity)
            .ToList();

        return new CompilationResult(
            emit.Success,
            emit.Success ? peStream.ToArray() : null,
            diagnostics);
    }

    private static CompileDiagnostic Map(Diagnostic d)
    {
        var line = d.Location.IsInSource
            ? d.Location.GetLineSpan().StartLinePosition.Line + 1
            : 0;

        return new CompileDiagnostic
        {
            Severity = d.Severity switch
            {
                Microsoft.CodeAnalysis.DiagnosticSeverity.Error => Contracts.Forge.DiagnosticSeverity.Error,
                Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => Contracts.Forge.DiagnosticSeverity.Warning,
                Microsoft.CodeAnalysis.DiagnosticSeverity.Info => Contracts.Forge.DiagnosticSeverity.Info,
                _ => Contracts.Forge.DiagnosticSeverity.Hidden
            },
            Code = d.Id,
            Message = d.GetMessage(),
            File = d.Location.IsInSource ? d.Location.SourceTree?.FilePath : null,
            Line = line
        };
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var tpa = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (var path in tpa)
        {
            var simpleName = Path.GetFileNameWithoutExtension(path);
            if (AllowedReferenceSimpleNames.Contains(simpleName, StringComparer.OrdinalIgnoreCase))
            {
                builder.Add(MetadataReference.CreateFromFile(path));
            }
        }

        return builder.ToImmutable();
    }
}

public sealed record CompilationResult(
    bool Success,
    byte[]? AssemblyImage,
    IReadOnlyList<CompileDiagnostic> Diagnostics)
{
    public IEnumerable<CompileDiagnostic> Errors =>
        Diagnostics.Where(d => d.Severity == Contracts.Forge.DiagnosticSeverity.Error);
}
