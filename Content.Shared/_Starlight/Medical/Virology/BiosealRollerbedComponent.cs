using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// Marks a one-person entity storage as a disease-isolating Bioseal Rollerbed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BiosealRollerbedComponent : Component
{
    /// <summary>
    /// Time a conscious occupant needs to unzip the cover from inside.
    /// </summary>
    [DataField]
    public TimeSpan InternalUnzipTime = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public DoAfterId? InternalUnzipDoAfter;
}

[Serializable, NetSerializable]
public sealed partial class BiosealInternalUnzipDoAfterEvent : SimpleDoAfterEvent;
