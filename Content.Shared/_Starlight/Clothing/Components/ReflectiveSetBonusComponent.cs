using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared.Tag;

namespace Content.Shared._Starlight.Clothing.Components;

/// <summary>
/// Component for items that are part of the reflective armor set.
/// Stores the original reflection probability from the prototype.
/// </summary>
[RegisterComponent, AutoGenerateComponentState]
public sealed partial class ReflectiveSetBonusComponent : Component
{
    /// <summary>
    /// The original reflection probability from the prototype.
    /// Used to restore the value when the set bonus is lost.
    /// </summary>
    [AutoNetworkedField]
    public float OriginalReflectProb;

    /// <summary>
    /// Tag identifying this as the vest piece of the set.
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype>? VestTag;

    /// <summary>
    /// Tag identifying this as the helmet piece of the set.
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype>? HelmetTag;
}
