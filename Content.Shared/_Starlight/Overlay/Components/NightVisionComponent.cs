using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Overlay.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(true)]
public sealed partial class NightVisionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active = true;

    /// <summary>
    /// Whether or not the night vision provided by this component is blocked by disabilities.
    /// Special organs like cybereyes bypass disabilities, while fleshy default organs won't.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool DisabilityBlockable = false;

    [DataField]
    public EntProtoId EffectPrototype = "EffectNightVision";

    public bool Clothes;
}

[RegisterComponent]
public sealed partial class ClothesNightVisionComponent : Component
{ }
