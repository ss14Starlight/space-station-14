using Content.Server._Starlight.NPC.Systems;

namespace Content.Server._Starlight.NPC.Components;

/// <summary>
/// Hostiles this NPC saw shutting themselves into a locker or crate, and the storage each one hides in.
/// The NPC keeps going after the storage until the hider leaves it.
/// </summary>
[RegisterComponent, Access(typeof(NPCHidingWitnessSystem))]
public sealed partial class NPCHidingWitnessComponent : Component
{
    [ViewVariables]
    public Dictionary<EntityUid, EntityUid> Hidden = [];
}
