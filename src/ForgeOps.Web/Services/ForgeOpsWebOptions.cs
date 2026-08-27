namespace ForgeOps.Web.Services;

/// <summary>
/// The frontend's entire configuration surface (ProjectForge.md §39): the public API
/// base URL, nothing more. No AI provider details, tunnel URLs or bridge tokens ever
/// reach the browser.
/// </summary>
public sealed class ForgeOpsWebOptions
{
    public string ApiBaseUrl { get; set; } = string.Empty;

    public bool HasApi => !string.IsNullOrWhiteSpace(ApiBaseUrl);
}
