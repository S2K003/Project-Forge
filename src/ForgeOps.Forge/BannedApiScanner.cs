using ForgeOps.Contracts.Forge;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ForgeOps.Forge;

/// <summary>
/// Deterministic static gate over generated source (ProjectForge.md §10, §2.2). If this
/// finds anything, the code is never executed — a human sees the evidence first. This is
/// the front-line defence; a curated reference set (see <see cref="RoslynCompiler"/>) and
/// a bounded child process are the layers behind it.
/// </summary>
public static class BannedApiScanner
{
    private static readonly (string Prefix, string Reason)[] BannedNamespaces =
    [
        ("System.IO", "filesystem access"),
        ("System.Net", "network access"),
        ("System.Diagnostics.Process", "process creation"),
        ("System.Reflection.Emit", "runtime code generation"),
        ("System.Runtime.InteropServices", "native interop"),
        ("System.Runtime.Loader", "assembly loading"),
        ("System.Runtime.CompilerServices.RuntimeHelpers", "runtime internals"),
        ("Microsoft.Win32", "registry / OS access"),
        ("System.Security.Principal", "OS identity"),
        ("System.Threading.Thread", "raw thread control"),
    ];

    private static readonly (string Token, string Reason)[] BannedIdentifiers =
    [
        ("Process", "process creation"),
        ("File", "filesystem access"),
        ("Directory", "filesystem access"),
        ("FileStream", "filesystem access"),
        ("Assembly", "assembly loading / reflection"),
        ("Activator", "dynamic activation"),
        ("AppDomain", "app-domain manipulation"),
        ("Marshal", "native interop"),
        ("GCHandle", "native interop"),
        ("DllImport", "native interop"),
        ("LibraryImport", "native interop"),
        ("HttpClient", "network access"),
        ("Socket", "network access"),
    ];

    public static IReadOnlyList<BannedApiFinding> Scan(IReadOnlyDictionary<string, string> sources)
    {
        var findings = new List<BannedApiFinding>();

        foreach (var (path, code) in sources)
        {
            var tree = CSharpSyntaxTree.ParseText(SourceText.From(code), path: path);
            var root = tree.GetCompilationUnitRoot();

            foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            {
                var name = directive.Name?.ToString() ?? string.Empty;
                var hit = BannedNamespaces.FirstOrDefault(b =>
                    name == b.Prefix || name.StartsWith(b.Prefix + ".", StringComparison.Ordinal));
                if (hit.Prefix is not null)
                {
                    findings.Add(Finding($"using {name}", hit.Reason, path, directive));
                }
            }

            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case QualifiedNameSyntax qn:
                    {
                        var full = qn.ToString();
                        var hit = BannedNamespaces.FirstOrDefault(b =>
                            full.StartsWith(b.Prefix + ".", StringComparison.Ordinal) || full == b.Prefix);
                        if (hit.Prefix is not null)
                        {
                            findings.Add(Finding(full, hit.Reason, path, qn));
                        }

                        break;
                    }

                    case IdentifierNameSyntax id:
                    {
                        var hit = BannedIdentifiers.FirstOrDefault(b => b.Token == id.Identifier.ValueText);
                        if (hit.Token is not null)
                        {
                            findings.Add(Finding(id.Identifier.ValueText, hit.Reason, path, id));
                        }

                        break;
                    }

                    case MemberAccessExpressionSyntax ma:
                    {
                        var expr = ma.ToString();
                        if (expr is "Environment.Exit" or "Environment.FailFast"
                            || expr.EndsWith(".Exit", StringComparison.Ordinal) && expr.Contains("Environment", StringComparison.Ordinal))
                        {
                            findings.Add(Finding(expr, "environment / exit control", path, ma));
                        }

                        break;
                    }

                    case AttributeSyntax attr:
                    {
                        var attrName = attr.Name.ToString();
                        if (attrName is "DllImport" or "LibraryImport" or "UnmanagedCallersOnly")
                        {
                            findings.Add(Finding($"[{attrName}]", "native interop", path, attr));
                        }

                        break;
                    }
                }
            }

            foreach (var token in root.DescendantTokens())
            {
                if (token.Kind() == SyntaxKind.UnsafeKeyword || token.Kind() == SyntaxKind.StackAllocKeyword
                    || token.Kind() == SyntaxKind.FixedKeyword)
                {
                    findings.Add(Finding(token.ValueText, "unsafe / unmanaged memory", path, token.Parent!));
                }
            }
        }

        // De-duplicate by (api, file, line).
        return findings
            .GroupBy(f => (f.Api, f.File, f.Line))
            .Select(g => g.First())
            .OrderBy(f => f.File)
            .ThenBy(f => f.Line)
            .ToList();
    }

    private static BannedApiFinding Finding(string api, string reason, string path, SyntaxNode node)
    {
        var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        return new BannedApiFinding
        {
            Api = api,
            Reason = reason,
            File = path,
            Line = line,
            Snippet = node.ToString().Trim().Replace('\n', ' ').Replace('\r', ' ')[..Math.Min(120, node.ToString().Trim().Length)]
        };
    }
}
