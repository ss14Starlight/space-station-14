using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Chemistry.ExternalContainerInjector;

/// <summary>
/// Component for Injectors that use solutions from inserted vials instead of internal solutions.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ExternalContainerInjectorComponent : Component
{
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 TransferAmount = FixedPoint2.New(5);

    [DataField]
    public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    /// <summary>
    /// The ID of the item slot that holds the vial.
    /// </summary>
    [DataField(required: true)]
    public string VialSlotId = string.Empty;

    /// <summary>
    /// The name of the solution to use from the inserted vial.
    /// </summary>
    [DataField]
    public string VialSolutionName = "beaker";

    /// <summary>
    /// Decides whether you can inject everything or just mobs.
    /// </summary>
    [AutoNetworkedField]
    [DataField(required: true)]
    public bool OnlyAffectsMobs = false;

    /// <summary>
    /// If this can draw from containers in mob-only mode.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public bool CanContainerDraw = true;

    /// <summary>
    /// Whether or not the injector is able to draw from containers or if it's a single use
    /// device that can only inject.
    /// </summary>
    [DataField]
    public bool InjectOnly = false;
} 