using ForgeOps.Contracts.Forge;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ForgeOps.Forge;

/// <summary>
/// Produces the deterministic <see cref="AuditReport"/> over generated code
/// (ProjectForge.md §2.2, §13). Banned-API findings or compile errors mean the code is
/// never executed.
/// </summary>
public sealed class GeneratedCodeAuditor
{
    private readonly RoslynCompiler _compiler;

    public GeneratedCodeAuditor(RoslynCompiler compiler) => _compiler = compiler;

    public AuditResult Audit(IReadOnlyList<GeneratedFile> authorFiles, int repairAttempts)
    {
        var authorSources = authorFiles.ToDictionary(f => f.Path, f => f.Content);
        var banned = BannedApiScanner.Scan(authorSources);

        var implFiles = authorFiles.Where(f => f.Role == GeneratedFileRole.Implementation).ToList();
        var implSources = new Dictionary<string, string>
        {
            ["__Contract.cs"] = GeneratedSources.Contract,
            ["__ForgeTestKit.cs"] = GeneratedSources.TestKit,
        };
        foreach (var f in implFiles)
        {
            implSources[f.Path] = f.Content;
        }

        var compile = _compiler.Compile("ForgeOps.Generated.Impl", implSources);
        var (archPassed, archNotes) = CheckArchitecture(implFiles);

        var verdict = (banned.Count, compile.Success, archPassed) switch
        {
            ( > 0, _, _) => AuditVerdict.Failed,
            (_, false, _) => AuditVerdict.Failed,
            (_, _, false) => AuditVerdict.PassedWithWarnings,
            _ when compile.Diagnostics.Any(d => d.Severity == Contracts.Forge.DiagnosticSeverity.Warning)
                => AuditVerdict.PassedWithWarnings,
            _ => AuditVerdict.Passed
        };

        var report = new AuditReport
        {
            Compiled = compile.Success,
            RepairAttempts = repairAttempts,
            Diagnostics = compile.Diagnostics,
            BannedApis = banned,
            ArchitecturePassed = archPassed,
            ArchitectureNotes = archNotes,
            Verdict = verdict
        };

        return new AuditResult(report, compile.Success ? compile.AssemblyImage : null);
    }

    private static (bool Passed, IReadOnlyList<string> Notes) CheckArchitecture(IReadOnlyList<GeneratedFile> implFiles)
    {
        var notes = new List<string>();
        var passed = true;
        var foundImplementation = false;

        foreach (var file in implFiles)
        {
            var root = CSharpSyntaxTree.ParseText(file.Content).GetCompilationUnitRoot();

            foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var implementsContract = cls.BaseList?.Types
                    .Any(t => t.Type.ToString().Contains("ILoyaltyService", StringComparison.Ordinal)) ?? false;
                if (!implementsContract)
                {
                    continue;
                }

                foundImplementation = true;

                if (!cls.Modifiers.Any(m => m.Kind() == SyntaxKind.SealedKeyword))
                {
                    notes.Add($"{cls.Identifier.ValueText} should be sealed (immutable-by-default intent, §36).");
                    passed = false;
                }

                var publicMutableField = cls.Members.OfType<FieldDeclarationSyntax>().Any(f =>
                    f.Modifiers.Any(m => m.Kind() == SyntaxKind.PublicKeyword)
                    && !f.Modifiers.Any(m => m.Kind() == SyntaxKind.ReadOnlyKeyword || m.Kind() == SyntaxKind.ConstKeyword));
                if (publicMutableField)
                {
                    notes.Add($"{cls.Identifier.ValueText} exposes a public mutable field (hidden state, §36).");
                    passed = false;
                }
            }

            var ns = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
            if (ns is not null && !ns.StartsWith("CustomerHub", StringComparison.Ordinal))
            {
                notes.Add($"Implementation is in namespace '{ns}', expected 'CustomerHub.*'.");
                passed = false;
            }
        }

        if (!foundImplementation)
        {
            notes.Add("No class implementing ILoyaltyService was found.");
            passed = false;
        }
        else if (notes.Count == 0)
        {
            notes.Add("Implements ILoyaltyService; sealed; no public mutable state; namespace CustomerHub.Loyalty.");
        }

        return (passed, notes);
    }
}

public sealed record AuditResult(AuditReport Report, byte[]? ImplementationImage);
