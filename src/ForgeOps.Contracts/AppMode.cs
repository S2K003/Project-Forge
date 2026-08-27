namespace ForgeOps.Contracts;

/// <summary>
/// ForgeOps ships with exactly two modes (ProjectForge.md §9A). Every screen must
/// make it unmistakable which one is active.
/// </summary>
public enum AppMode
{
    /// <summary>Real backend, real database, real AI Bridge call to the local model.</summary>
    Live,

    /// <summary>Seeded fixtures, no live dependency, clearly-labelled simulated AI output.</summary>
    Demo
}
