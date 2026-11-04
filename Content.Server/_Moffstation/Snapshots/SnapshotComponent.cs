using Content.Shared._Moffstation.Snapshots;

namespace Content.Server._Moffstation.Snapshots;

/// <summary>
/// The scene a snapshot shows. Saved Serverside and send when requested
/// </summary>
[RegisterComponent]
public sealed partial class SnapshotComponent : Component
{
    [ViewVariables]
    public SnapshotData? Data;
}
