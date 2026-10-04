using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Xenoarchaeology.Artifact.XAE.Components;

/// <summary>
/// Temporarily turns the artifact into a turret that shoots nearby living targets
/// </summary>
[RegisterComponent, Access(typeof(XAETurretSystem))]
public sealed partial class XAETurretComponent : Component
{
    /// <summary>
    /// Weighted list of projectiles
    /// </summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, float> PossibleProjectiles = new();

    /// <summary>
    /// The projectile picked for this node
    /// </summary>
    [DataField]
    public EntProtoId? SelectedProjectile;

    /// <summary>
    /// Turret transformation duration
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Shots per second
    /// </summary>
    [DataField]
    public float FireRate = 2f;

    /// <summary>
    /// Turret range
    /// </summary>
    [DataField]
    public float Range = 8f;

    /// <summary>
    /// Turret faction. Defaults to hostile to all
    /// </summary>
    [DataField]
    public ProtoId<NpcFactionPrototype> Faction = "AllHostile";
}
