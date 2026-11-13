using Content.Shared._Starlight.Language;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xeno.Components;

/// <summary>
/// Added to entities to tag that they are a participant in Xemocracy
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class XenoDemocracyComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId XenoDemocracyVoteAction = "ActionXenoVote";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? CurrentVoteHolder;

    [ViewVariables, AutoNetworkedField]
    public bool IsVoice = false;

    [DataField]
    public ProtoId<LanguagePrototype> NonVoiceLanguage = "Xenocongress";

    [DataField]
    public ProtoId<LanguagePrototype> VoiceLanguage = "Xenoqueen";

}
