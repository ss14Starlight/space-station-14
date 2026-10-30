using Robust.Client.Input;
using Robust.Shared.Input;

namespace Content.Client._Starlight.Replay;

// The engine's HideUI toggle hides UI controls but not world overlays, so the player overlay follows it here.
// Status icons deliberately don't; they have their own toggle.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private IInputManager _input = default!;

    private bool IsHudHidden()
    {
        foreach (var binding in _input.GetKeyBindings(EngineKeyFunctions.HideUI))
        {
            if (binding.State == BoundKeyState.Down)
                return true;
        }

        return false;
    }
}
