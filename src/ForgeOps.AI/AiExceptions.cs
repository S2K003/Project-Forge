namespace ForgeOps.AI;

/// <summary>Base type for AI failures the API surfaces distinctly (ProjectForge.md §45).</summary>
public abstract class AiException : Exception
{
    protected AiException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// The developer's PC / tunnel is unreachable — an <b>expected</b> state (§7A.3, §44).
/// Handled differently from a model or timeout failure: in Live Mode it locks the app
/// behind the connection gate (§9A.1).
/// </summary>
public sealed class AiBridgeUnreachableException : AiException
{
    public AiBridgeUnreachableException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>The bridge answered but the model errored, timed out, or returned unusable output.</summary>
public sealed class AiModelException : AiException
{
    public AiModelException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>The circuit breaker is open after repeated bridge failures (§7.2).</summary>
public sealed class AiCircuitOpenException : AiException
{
    public AiCircuitOpenException(string message) : base(message) { }
}
