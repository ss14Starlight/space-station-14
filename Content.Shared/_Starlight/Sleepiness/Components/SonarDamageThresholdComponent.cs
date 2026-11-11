using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Sleepiness.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SonarDamageThresholdComponent : Component
{
    public FixedPoint2 DestructionThreshold;
}

[Serializable, NetSerializable]
public sealed class SonarDamageThresholdComponentState(FixedPoint2 destructionThreshold) : ComponentState
{
    public readonly FixedPoint2 DestructionThreshold = destructionThreshold;
}
