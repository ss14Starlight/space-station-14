using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.StatusIcon;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Antags.BloodBound.Components;

/// <summary>
/// A member of a blood oath. Unpaired members selected by the game rule may recruit one partner.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BloodBoundComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Partner;

    [DataField]
    public ProtoId<FactionIconPrototype> BloodBoundIcon = "BloodBoundFaction";

    /// <summary>
    /// Grants recruitment actions until a partner is bound. Recruited members do not receive them.
    /// </summary>
    [DataField]
    public bool CanRecruit;

    [DataField]
    public EntProtoId<EntityTargetActionComponent> ConvertAction = "ActionBloodBoundConvert";

    [DataField]
    public EntityUid? ConvertActionEntity;

    [DataField]
    public EntProtoId<EntityTargetActionComponent> CheckConvertAction = "ActionBloodBoundCheckConvert";

    [DataField]
    public EntityUid? CheckConvertActionEntity;

    /// <summary>
    /// Required antag preference for recruitment unless the target has the Blood Bound trait.
    /// Null skips this check.
    /// </summary>
    [DataField]
    public ProtoId<AntagPrototype>? RequiredAntagPreference = "BloodBound";

    [DataField]
    public LocId ConvertPopupText = "blood-bound-conversion-popup";

    [DataField]
    public TimeSpan? ConvertStunTime = TimeSpan.FromSeconds(3);

    [DataField]
    public EntProtoId<ObjectiveComponent> AssistObjective = "BloodBoundAssistObjective";

    [DataField(required: true)]
    public EntProtoId<ObjectiveComponent> EscapeObjective;

    [DataField]
    public LocId BriefingText = "blood-bound-role-greeting";

    [DataField]
    public Color BriefingColor = Color.MediumVioletRed;

    [DataField]
    public SoundSpecifier BriefingSound = new SoundPathSpecifier("/Audio/_Harmony/Misc/blood_brother_greeting.ogg");

    [DataField]
    public ProtoId<NpcFactionPrototype> BloodBoundFaction = "BloodBound";

    [DataField]
    public EntProtoId<MindRoleComponent> BloodBoundMindRole = "MindRoleBloodBound";

    public override bool SessionSpecific => true;
}

public sealed partial class BloodBoundConvertActionEvent : EntityTargetActionEvent;

public sealed partial class BloodBoundCheckConvertActionEvent : EntityTargetActionEvent;
