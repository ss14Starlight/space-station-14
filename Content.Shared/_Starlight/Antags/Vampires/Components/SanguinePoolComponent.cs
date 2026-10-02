using Robust.Shared.Audio;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
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

    [DataField]
    public ProtoId<ReagentPrototype> TrailReagent = "Blood";

    [DataField]
    public FixedPoint2 TrailReagentQuantity = FixedPoint2.New(30);

    [ViewVariables]
    public (EntityUid Grid, Vector2i Tile)? LastTrail;
}
