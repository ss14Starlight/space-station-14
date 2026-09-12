using Robust.Shared.Serialization;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// One-off remarks the coach makes when a player does something not intended.
/// </summary>
[RegisterComponent]
public sealed partial class TutorialQuipComponent : Component
{
    [DataField]
    public List<TutorialQuip> Quips = new();
}

[DataDefinition]
public sealed partial class TutorialQuip
{
    [DataField(required: true)]
    public TutorialQuipTrigger Trigger;

    [DataField(required: true)]
    public LocId Line = default!;

    /// <summary>Said once.</summary>
    [ViewVariables]
    public bool Spoken;
}

[Serializable, NetSerializable]
public enum TutorialQuipTrigger : byte
{
    /// <summary>Player ate or drank this.</summary>
    Ingested,

    /// <summary>Player put themselves inside this.</summary>
    PlayerInserted,
}
