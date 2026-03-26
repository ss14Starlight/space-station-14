using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Antags.Vampires.Components;

/// <summary>
///     Marker placed on the polymorph form created by Sanguine Pool.
///     Handles collision filtering on both client and server and exposes
///     tunables used while the form is active (trail spawning, etc.).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SanguinePoolComponent : Component
{
    /// <summary>
    ///     Prototype spawned when the pool enters a new tile
    /// </summary>
    [DataField]
    public EntProtoId? TrailPrototype = "PuddleBlood";

    [DataField]
    public EntProtoId ExitEffectPrototype = "VampireSanguinePoolIn";

    [DataField]
    public SoundSpecifier ExitSound = new SoundPathSpecifier("/Audio/_Starlight/Effects/vampire/exit_blood.ogg");

    [ViewVariables]
    public (EntityUid Grid, Vector2i Tile)? LastTrail;
}
