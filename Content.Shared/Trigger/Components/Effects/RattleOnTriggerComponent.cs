using Content.Shared.Mobs;
using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Audio; // Starlight
using Robust.Shared.Prototypes;

namespace Content.Shared.Trigger.Components.Effects;

/// <summary>
/// Sends an emergency message over coms when triggered giving information about the entity's mob status.
/// If TargetUser is true then the user's mob state will be used instead.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RattleOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// The radio channel the message will be sent to.
    /// Starlight: Converted into a list.
    /// </summary>
    [DataField("radioChannel", required: false)]
    public List<ProtoId<RadioChannelPrototype>> RadioChannel = new()
    {
        "Syndicate"
    };

    /// <summary>
    /// The message to be send depending on the target's current mob state.
    /// </summary>
    [DataField]
    public Dictionary<MobState, LocId> Messages = new()
    {
        {MobState.Critical, "rattle-on-trigger-critical-message"},
        {MobState.Dead, "rattle-on-trigger-dead-message"}
    };
    #region Starlight
    /// <summary>
    /// Announce on all channels
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Global = false;

    /// <summary>
    /// Fluent ID for the declaration sender title
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public LocId SenderTitle = "comms-console-announcement-title-centcom";

    /// <summary>
    /// Announce sound file path
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");

    /// <summary>
    /// Announcement color
    /// </summary>
    [ViewVariables]
    [DataField, AutoNetworkedField]
    public Color Color = Color.Gold;
    #endregion Starlight
}
