using ForgeOps.Contracts.Forge;

namespace ForgeOps.Forge;

/// <summary>
/// Deterministic routing of a requirement to a generation target (ProjectForge.md §2.2 —
/// use a deterministic tool where one does the job). A UI-shaped requirement produces a
/// web component that is rendered; everything else produces C# logic that is executed.
/// </summary>
public static class RequirementClassifier
{
    private static readonly string[] UiSignals =
    [
        "ui", "screen", "page", "view", "layout", "component", "widget", "card", "badge",
        "button", "form", "input", "modal", "dialog", "dropdown", "menu", "navbar", "sidebar",
        "dashboard", "chart", "graph", "table", "list view", "grid", "gallery", "carousel",
        "display", "show", "render", "visual", "design", "style", "theme", "dark mode",
        "light mode", "responsive", "landing", "hero", "banner", "tooltip", "toast",
        "progress bar", "spinner", "avatar", "html", "css", "tailwind", "front end",
        "frontend", "web page", "webpage", "mockup", "wireframe", "colour", "color scheme",
    ];

    private static readonly string[] LogicOverrides =
    [
        "api endpoint", "database", "webhook", "background job", "algorithm", "calculate",
        "idempoten", "concurrency", "migration", "repository", "service class",
    ];

    public static ImplementationKind Classify(string requirementText, string? specSummary = null)
    {
        var text = ((requirementText ?? string.Empty) + " " + (specSummary ?? string.Empty)).ToLowerInvariant();

        var logicHits = LogicOverrides.Count(s => text.Contains(s, StringComparison.Ordinal));
        var uiHits = UiSignals.Count(s => text.Contains(s, StringComparison.Ordinal));

        return uiHits > logicHits && uiHits > 0
            ? ImplementationKind.WebComponent
            : ImplementationKind.CSharpLogic;
    }
}
