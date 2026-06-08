using Content.Server.Administration.Logs;  // Starlight
using Content.Server.Radio.EntitySystems;
using Content.Server.Pinpointer;
using Content.Shared.Mobs.Components;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Content.Server.Chat.Systems; // Starlight
using Content.Shared.Database; // Starlight
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Trigger.Systems;

public sealed class RattleOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly ChatSystem _chat = default!; // Starlight
    [Dependency] private readonly IAdminLogManager _adminLogger = default!; // Starlight

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RattleOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<RattleOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var target = ent.Comp.TargetUser ? args.User : ent.Owner;

        if (target == null)
            return;

        if (!TryComp<MobStateComponent>(target.Value, out var mobstate))
            return;

        args.Handled = true;

        if (!ent.Comp.Messages.TryGetValue(mobstate.CurrentState, out var messageId))
            return;

        if (ent.Comp.RadioChannel == null) //starlight
            return;

        // Gets the location of the user
        var posText = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(target.Value));

        var message = Loc.GetString(messageId, ("user", target.Value), ("position", posText));
        #region Starlight
        //For global announcements, ie, DAGD nuke codes, so they can't just sabotage comms. Could also use this to announce when someone important is dead.
        if (ent.Comp.Global)
        {
            var title = Loc.GetString(ent.Comp.SenderTitle);
            _chat.DispatchGlobalAnnouncement(message, title, true, ent.Comp.Sound, ent.Comp.Color);
            var actor = args.User ?? ent.Owner;
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"{ToPrettyString(actor):player} has triggered the following rattle: {message}");
            return;
        }

        // Sends a message to the radio channels specified by the implant
        foreach (var radioChannel in ent.Comp.RadioChannel)
        {
            _radio.SendRadioMessage(ent.Owner, message, _prototypeManager.Index(radioChannel), ent.Owner); //Starlight swapped ent.Comp.RadioChannel for radioChannel
        }
        #endregion
    }
}
