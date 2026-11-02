using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Objectives.ObjectivePicker;

/// <summary>
/// Tracks the window's lifetime so only visible pickers receive periodic availability refreshes.
/// </summary>
[Serializable, NetSerializable]
public sealed class ObjectivePickerSetOpen : EntityEventArgs
{
    public NetEntity MindId;
    public bool Open;
}

[Serializable, NetSerializable]
public sealed class ObjectivePickerMulligan : EntityEventArgs
{
    public NetEntity MindId;
    public NetEntity RetainedObjective;
}

[Serializable, NetSerializable]
public sealed class ObjectivePickerReply : EntityEventArgs
{
    public bool Accepted;
    public bool Finished;
    public bool OpenPicker;
    public string? Message;
}

/// <summary>
///     Clients listen for this event and when they get it, they open a popup so the player can fill out the objective summary.
/// </summary>
[Serializable, NetSerializable]
public sealed class ObjectivePickerOpenMessage : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class ObjectivePickerSelected : EntityEventArgs
{
    public NetEntity MindId;
    public HashSet<NetEntity> SelectedObjectives = new();
}

[Serializable, NetSerializable]
public sealed class ObjectivePickerRequestAdditional : EntityEventArgs
{
    public NetEntity MindId;
}
