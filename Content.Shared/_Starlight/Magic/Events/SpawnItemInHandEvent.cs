using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Magic.Events;

public sealed partial class SpawnItemInHandEvent : InstantActionEvent
{
    /// <summary>
    /// Item to create with the action
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Spawned = string.Empty;
}

/// <summary>
///     Raised after an entity was spawned in someones hand.
/// </summary>
public sealed partial class AfterSpawnItemInHandEvent
{
    /// <summary>
    ///     Entity that was spawned.
    /// </summary>
    [DataField]
    public EntityUid Entity;
    
    /// <summary>
    ///     The entity who spawned the item.
    /// </summary>
    [DataField]
    public EntityUid Performer;
}
