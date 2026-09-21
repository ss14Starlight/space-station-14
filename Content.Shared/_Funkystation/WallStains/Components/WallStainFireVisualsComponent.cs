using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Funkystation.WallStains.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class WallStainFireVisualsComponent : Component
{
}

[Serializable, NetSerializable]
public enum WallStainFireVisuals : byte
{
    FireState,
    FireColor
}
