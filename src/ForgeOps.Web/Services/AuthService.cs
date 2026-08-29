using System.Security.Cryptography;
using System.Text;
using Microsoft.JSInterop;

namespace ForgeOps.Web.Services;

/// <summary>
/// The front-door gate for the ForgeOps console. This is a client-side check — the app is
/// a static WASM bundle with no auth server — so it is a lock on the door, not cryptographic
/// protection of a secret. The credential is never stored in the bundle in the clear: only
/// a PBKDF2-SHA256 derivation of "<c>username:password</c>" is compiled in, and the entered
/// value is compared to it in fixed time. The session marker lives in <c>sessionStorage</c>,
/// so it clears when the browser tab closes.
/// </summary>
public sealed class AuthService
{
    private const string StorageKey = "forgeops.gate";
    private const string Salt = "forgeops.gate.v1";
    private const int Iterations = 50_000;

    // PBKDF2-SHA256( "adamcogan:adamcogan", "forgeops.gate.v1", 50000, 32 )
    private const string ExpectedHex = "419475295ad3d55e67815bd20944f9dc40904b6360a8053ce2e3439b9fa81f1a";

    private readonly IJSRuntime _js;

    public AuthService(IJSRuntime js) => _js = js;

    public bool Initialized { get; private set; }

    public bool IsAuthenticated { get; private set; }

    public event Action? Changed;

    /// <summary>Read any existing session marker. Safe to call more than once.</summary>
    public async Task InitializeAsync()
    {
        if (Initialized)
        {
            return;
        }

        string? marker = null;
        try
        {
            marker = await _js.InvokeAsync<string?>("forgeAuth.get", StorageKey);
        }
        catch
        {
            // storage blocked / unavailable — treat as signed out
        }

        IsAuthenticated = string.Equals(marker, ExpectedHex, StringComparison.Ordinal);
        Initialized = true;
        Changed?.Invoke();
    }

    /// <summary>
    /// Validate a username / password pair. On success the session marker is written and the
    /// app becomes accessible until the tab is closed or <see cref="LogoutAsync"/> is called.
    /// </summary>
    public async Task<bool> TryLoginAsync(string? username, string? password)
    {
        var user = (username ?? string.Empty).Trim().ToLowerInvariant();
        var pass = password ?? string.Empty;

        if (user.Length == 0 || pass.Length == 0)
        {
            return false;
        }

        var derived = Rfc2898DeriveBytes.Pbkdf2(
            $"{user}:{pass}", Encoding.UTF8.GetBytes(Salt), Iterations, HashAlgorithmName.SHA256, 32);

        var ok = CryptographicOperations.FixedTimeEquals(derived, Convert.FromHexString(ExpectedHex));
        if (!ok)
        {
            return false;
        }

        IsAuthenticated = true;
        try
        {
            await _js.InvokeVoidAsync("forgeAuth.set", StorageKey, ExpectedHex);
        }
        catch
        {
            // storage blocked — session still works for this page load
        }

        Changed?.Invoke();
        return true;
    }

    public async Task LogoutAsync()
    {
        IsAuthenticated = false;
        try
        {
            await _js.InvokeVoidAsync("forgeAuth.clear", StorageKey);
        }
        catch
        {
            // ignore
        }

        Changed?.Invoke();
    }
}
