using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Inventory;

/// <summary>
/// A collar made for animals. It only fits inventory slots whose whitelist names <see cref="SlotTag"/>,
/// like a K9's or smart corgi's ID slot.
/// </summary>
[RegisterComponent]
public sealed partial class AnimalCollarComponent : Component
{
    [DataField]
    public ProtoId<TagPrototype> SlotTag = "CorgiWearable";
}
