using Content.Shared._Starlight.Medical.Limbs;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
namespace Content.Shared.Starlight;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimbWithActionComponent : Component, IWithAction
{
    [DataField, AutoNetworkedField]
    public bool EntityIcon { get; set; } = false;

    [DataField(required: true), AutoNetworkedField]
    public EntProtoId Action { get; set; } = "ActionScream";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity { get; set; }

    [DataField, AutoNetworkedField]
    public SoundSpecifier? Sound;
}
