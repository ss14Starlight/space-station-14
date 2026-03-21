using Content.Shared.Paper;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Paper;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class MultistampComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public List <EntityUid> Stamps = new();

    [ViewVariables, AutoNetworkedField]
    public int CurrentEntry = 0;

    [ViewVariables, AutoNetworkedField]
    public string CurrentStampName = string.Empty;

    [ViewVariables, AutoNetworkedField]
    public bool UiUpdateNeeded;

    [DataField]
    public bool StatusShowStamp = true;

    [DataField]
    public SoundSpecifier? ChangeSound = new SoundPathSpecifier("/Audio/Machines/lightswitch.ogg");
}
