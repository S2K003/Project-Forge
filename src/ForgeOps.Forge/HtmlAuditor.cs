using System.Text.RegularExpressions;
using ForgeOps.Contracts.Forge;

namespace ForgeOps.Forge;

/// <summary>
/// Deterministic gate over a generated web component (ProjectForge.md §10, §2.2). The
/// component is rendered in an iframe with <c>sandbox="allow-scripts"</c> and a strict CSP,
/// so it already cannot reach the app, cookies, storage or the network — this scan is the
/// front-line layer that also blocks the obvious escape attempts before rendering.
/// </summary>
public static partial class HtmlAuditor
{
    private static readonly (Regex Pattern, string Api, string Reason)[] Banned =
    [
        (ExternalSrc(), "external resource", "loads code/markup from another origin"),
        (FetchOrXhr(), "fetch / XMLHttpRequest", "network access"),
        (WebSocketRx(), "WebSocket / EventSource", "network access"),
        (DynamicImport(), "import()", "loads code at runtime"),
        (EvalRx(), "eval / new Function", "runtime code evaluation"),
        (ParentAccess(), "window.parent / window.top", "attempts to reach the host page"),
        (StorageCookie(), "cookies / storage", "reads or writes browser storage"),
        (NestedFrame(), "nested iframe / object / embed", "embeds another browsing context"),
        (Navigation(), "location assignment", "navigates away"),
        (ServiceWorker(), "serviceWorker / Notification / geolocation", "privileged browser API"),
    ];

    public static IReadOnlyList<BannedApiFinding> Scan(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [new BannedApiFinding { Api = "(empty)", Reason = "no document was produced", File = "index.html", Line = 0 }];
        }

        var findings = new List<BannedApiFinding>();
        var lines = html.Replace("\r\n", "\n").Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            foreach (var (pattern, api, reason) in Banned)
            {
                var m = pattern.Match(lines[i]);
                if (m.Success)
                {
                    findings.Add(new BannedApiFinding
                    {
                        Api = api,
                        Reason = reason,
                        File = "index.html",
                        Line = i + 1,
                        Snippet = m.Value.Trim()[..Math.Min(120, m.Value.Trim().Length)]
                    });
                }
            }
        }

        return findings
            .GroupBy(f => (f.Api, f.Line))
            .Select(g => g.First())
            .OrderBy(f => f.Line)
            .ToList();
    }

    /// <summary>Structural sanity: a single self-contained document with a body.</summary>
    public static (bool Ok, IReadOnlyList<string> Notes) CheckStructure(string html)
    {
        var notes = new List<string>();
        var ok = true;

        if (!html.Contains("<body", StringComparison.OrdinalIgnoreCase)
            && !html.Contains("<div", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add("Document has no <body> or block content.");
            ok = false;
        }

        if (LinkTagRx().IsMatch(html))
        {
            notes.Add("Contains a <link> tag — the component must be fully self-contained (inline CSS only).");
            ok = false;
        }

        if (ok)
        {
            notes.Add("Self-contained document; inline styles/scripts only; no external resources.");
        }

        return (ok, notes);
    }

    [GeneratedRegex(@"(<(script|img|source|iframe|video|audio|link)\b[^>]*\b(src|href)\s*=\s*[""']?\s*(https?:|//))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalSrc();

    [GeneratedRegex(@"\b(fetch\s*\(|XMLHttpRequest|navigator\.sendBeacon)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FetchOrXhr();

    [GeneratedRegex(@"\b(new\s+WebSocket|new\s+EventSource)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WebSocketRx();

    [GeneratedRegex(@"\bimport\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DynamicImport();

    [GeneratedRegex(@"(\beval\s*\(|new\s+Function\s*\(|setTimeout\s*\(\s*[""'])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EvalRx();

    [GeneratedRegex(@"\bwindow\s*\.\s*(parent|top|opener)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ParentAccess();

    [GeneratedRegex(@"\b(document\.cookie|localStorage|sessionStorage|indexedDB)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StorageCookie();

    [GeneratedRegex(@"<(iframe|object|embed)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NestedFrame();

    [GeneratedRegex(@"\b(location\s*\.\s*(href|assign|replace)\s*=|location\s*=)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Navigation();

    [GeneratedRegex(@"\bnavigator\s*\.\s*(serviceWorker|geolocation|clipboard|mediaDevices)|new\s+Notification", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ServiceWorker();

    [GeneratedRegex(@"<link\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinkTagRx();
}
