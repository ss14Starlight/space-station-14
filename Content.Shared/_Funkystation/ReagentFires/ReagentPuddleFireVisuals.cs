using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Funkystation.ReagentFires
{
    [Serializable, NetSerializable]
    public enum ReagentPuddleFireVisuals : byte
    {
        OnFire,
        FireState,
        FireColor
    }

    [RegisterComponent, NetworkedComponent]
    public sealed partial class ReagentPuddleFireEffectComponent : Component
    {
    }
}
