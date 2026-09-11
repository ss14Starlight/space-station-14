using Content.Shared.Alert;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Actions.Components;

/// <summary>
/// Component that allows an entity to enter and exit stasis.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShellComponent : Component
{
    /// <summary>
    /// The entity needed to snap off a pice of your shell.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId GenerateShellPieceAction;

    [DataField, AutoNetworkedField]
    public EntityUid? GenerateShellPieceActionEntity;

    [DataField]
    public ComponentRegistry? NoShellComponents;

    /// <summary>
    /// The alert for notifying the shelled creature about the integrity of their shell.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> ShellAlert = "DollShellIntegrity";

    [ViewVariables]
    public List<Marking> OriginalMarkings = [];

    [DataField]
    public ProtoId<DamageGroupPrototype> DestroyedBy = "Brute";

    [DataField]
    public float Stability = 1f;

    [DataField]
    public float Hardness = 15f;

    [DataField]
    public SoundSpecifier? ShellBreakSound = new SoundPathSpecifier("/Audio/Effects/metal_glass_break1.ogg", new AudioParams(1f, 2f, 5f, 1, 1, false, 0f, 2f));
}
