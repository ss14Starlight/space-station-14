using Content.Shared.RCD.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Content.Shared.Atmos.Components; // Starlight: RPD

namespace Content.Shared.RCD.Components;

/// <summary>
/// Main component for the RCD
/// Optionally uses LimitedChargesComponent.
/// Charges can be refilled with RCD ammo
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(RCDSystem))]
public sealed partial class RCDComponent : Component
{
    /// <summary>
    /// List of RCD prototypes that the device comes loaded with
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<RCDPrototype>> AvailablePrototypes { get; set; } = new();

    /// <summary>
    /// Sound that plays when a RCD operation successfully completes
    /// </summary>
    [DataField]
    public SoundSpecifier SuccessSound { get; set; } = new SoundPathSpecifier("/Audio/Items/deconstruct.ogg");

    /// <summary>
    /// The ProtoId of the currently selected RCD prototype
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<RCDPrototype> ProtoId { get; set; } = "Invalid";

    // Starlight Start: RPD
    /// <summary>
    /// Indicates whether this is an RPD
    /// </summary>
    [DataField("isRPD"), AutoNetworkedField]
    public bool IsRPD { get; set; } = false;

    /// <summary>
    /// When true the RCD will use the prototype's MirrorPrototype (if available) for placement/validation.
    /// This is networked so the server can validate/finalize mirror placement.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public bool UseMirrorPrototype { get; set; } = false;

    /// <summary>
    /// Selected pipe layer when placing atmos devices with the RPD.
    /// </summary>
    [DataField, AutoNetworkedField]
    public AtmosPipeLayer SelectedPipeLayer { get; set; } = AtmosPipeLayer.Primary;
    // Starlight End

    /// <summary>
    /// The direction constructed entities will face upon spawning
    /// </summary>
    [DataField, AutoNetworkedField]
    public Direction ConstructionDirection
    {
        get => _constructionDirection;
        set
        {
            _constructionDirection = value;
            ConstructionTransform = new Transform(new(), _constructionDirection.ToAngle());
        }
    }

    private Direction _constructionDirection = Direction.South;

    /// <summary>
    /// Returns a rotated transform based on the specified ConstructionDirection
    /// </summary>
    /// <remarks>
    /// Contains no position data
    /// </remarks>
    [ViewVariables(VVAccess.ReadOnly)]
    public Transform ConstructionTransform { get; private set; }
}
