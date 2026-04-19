using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Actions.Components;

[RegisterComponent]
public sealed partial class WrappedComponent : Component
{
    [DataField]
    public TimeSpan UnWrapTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan SelfUnWrapTime = TimeSpan.FromSeconds(15);

    [DataField]
    public EntProtoId WrappedEffectId = "EffectTerrorCocoon";

    public EntityUid? EffectEntity = null;
}

[Serializable, NetSerializable]
public enum WrappedVisuals : byte
{
    IsWrapped,
}
