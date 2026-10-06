using Content.Server._Starlight.NPC.Systems;

namespace Content.Server._Starlight.NPC.Components;

/// <summary>
/// Marks an NPC that <see cref="NPCProximitySleepSystem"/> put to sleep because no player was nearby.
/// Only NPCs with this marker are woken back up by that system, so sleeps for other reasons (death, minds) are left alone.
/// </summary>
[RegisterComponent, Access(typeof(NPCProximitySleepSystem))]
public sealed partial class NPCProximityDormantComponent : Component;

/// <summary>
/// Keeps this NPC running even with no player around, e.g. point defense that has to react to things on its own.
/// </summary>
[RegisterComponent]
public sealed partial class NPCProximitySleepExemptComponent : Component;
