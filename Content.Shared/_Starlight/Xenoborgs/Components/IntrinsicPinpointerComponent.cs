using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Starlight.Xenoborgs.Components;

/// <summary>
/// When placed on a mob, shows a persistent HUD arrow on the local player's screen
/// pointing toward the nearest entity that has the specified component type.
/// Cannot be dropped or removed. No physical item is spawned.
/// All key fields are VV-writable at runtime so admins can retarget it in-game.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IntrinsicPinpointerComponent : Component
{
    /// <summary>
    /// The name of the component type to search for as the target.
    /// Change this in VV to retarget the arrow (e.g. "MothershipCore", "ResearchServer").
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public string? Component;

    /// <summary>
    /// Label shown alongside the HUD arrow.
    /// Change this in VV to update what the arrow calls the beacon.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public string TargetName = "the Mothership";

    /// <summary>Distance threshold (tiles) at which we switch from Far to Medium.</summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float MediumDistance = 32f;

    /// <summary>Distance threshold (tiles) at which we switch from Medium to Close.</summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float CloseDistance = 8f;

    /// <summary>Distance threshold (tiles) at which we show "Reached".</summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float ReachedDistance = 1.5f;

    /// <summary>
    /// World position of the target entity. Null when no target is found.
    /// Networked to the client so the overlay can compute the direction arrow directly.
    /// </summary>
    [AutoNetworkedField]
    public Vector2? TargetWorldPos;

    /// <summary>
    /// Map of the target entity. Null when no target found.
    /// Used by the client to show null icon when the target is on a different map.
    /// </summary>
    [AutoNetworkedField]
    public MapId? TargetMapId;

}
