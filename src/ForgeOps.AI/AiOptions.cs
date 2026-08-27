namespace ForgeOps.AI;

/// <summary>
/// Strongly-typed AI configuration (ProjectForge.md §39). Bound from the "Ai" section.
/// In the deployed topology <see cref="BaseUrl"/> is the AI Bridge tunnel URL, not localhost.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>"OllamaBridge" (default) or "Mock".</summary>
    public string Provider { get; set; } = "OllamaBridge";

    /// <summary>Ollama base URL, reached directly in dev or through the authenticated tunnel when deployed.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    public string Model { get; set; } = "qwen3:8b";

    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Shared bridge token sent as a bearer header to the tunnel. Never exposed to the browser (§39).</summary>
    public string? BridgeToken { get; set; }

    /// <summary>Short timeout for the health probe so a hung bridge cannot hang <c>/health/ai-bridge</c> (§9A.1).</summary>
    public int ProbeTimeoutSeconds { get; set; } = 4;

    /// <summary>Consecutive failures that open the circuit breaker.</summary>
    public int CircuitFailureThreshold { get; set; } = 3;

    /// <summary>Seconds the breaker stays open before a trial request is allowed.</summary>
    public int CircuitResetSeconds { get; set; } = 30;
}
