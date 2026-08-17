using Content.Shared.Preferences.Loadouts;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Humanoid;

/// <summary>
/// Component for Neocytes, a hybrid species that requires a frame to be equipped. Handles their frame loadout.
/// </summary>
[RegisterComponent]
public sealed partial class NeocyteComponent : Component
{
    /// <summary>
    /// The loadout group whose entries will be randomly selected to provide a frame if the Neocyte has no frame equipped.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<LoadoutGroupPrototype> FrameLoadoutGroup;

    /// <summary>
    /// The inventory slot occupied by a frame.
    /// </summary>
    [DataField(required: true)]
    public string FrameSlot = string.Empty;

    /// <summary>
    /// The last frame equipped to this Neocyte. Kept while polymorphed so the same frame can be restored.
    /// </summary>
    [ViewVariables]
    public EntProtoId? LastFramePrototype;
}
