using ForgeOps.Contracts;

namespace ForgeOps.Web.Services;

/// <summary>
/// Tracks the active application mode (ProjectForge.md §9A). Switching is always
/// explicit — driven by the route the user navigates to (<c>/demo</c> or <c>/live</c>),
/// never inferred silently from connection state.
/// </summary>
public sealed class AppModeService
{
    private AppMode? _mode;

    public AppMode? Current => _mode;

    public bool IsChosen => _mode is not null;

    public event Action? Changed;

    public void Set(AppMode mode)
    {
        if (_mode == mode)
        {
            return;
        }

        _mode = mode;
        Changed?.Invoke();
    }

    public void Clear()
    {
        _mode = null;
        Changed?.Invoke();
    }
}
