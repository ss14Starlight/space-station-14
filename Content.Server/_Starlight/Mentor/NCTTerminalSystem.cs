using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Mentor;

public sealed class NCTTerminalSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly IdExaminableSystem _idExaminableSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NCTTerminalComponent, ActivateInWorldEvent>(OnInteracted);
    }

    private void OnInteracted(EntityUid uid, NCTTerminalComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (!TryComp<ActorComponent>(args.User, out var actor) || actor.PlayerSession is null)
        {
            args.Handled = true;
            return;
        }

        if (!_power.IsPowered(uid))
        {
            _popupSystem.PopupEntity(Loc.GetString("base-computer-ui-component-not-powered", ("machine", uid)), uid, actor.PlayerSession);
            args.Handled = true;
            return;
        }

        var nameAndJob = _idExaminableSystem.GetInfo(args.User);
        if (nameAndJob is null)
        {
            _popupSystem.PopupEntity(Loc.GetString("nctterminal-noaccess"), uid, actor.PlayerSession, PopupType.Large);
            args.Handled = true;
            return;
        }

        var activeAgent = 0;
        var query = EntityQueryEnumerator<NCTAgentComponent>();
        while (query.MoveNext(out var agent, out var _))
        {
            if (_mobState.IsDead(agent))
                continue;

            activeAgent += 1;
        }

        if (activeAgent == 0)
        {
            _popupSystem.PopupEntity(Loc.GetString("nctterminal-noagent"), uid, actor.PlayerSession, PopupType.Large);
            args.Handled = true;
            return;
        }

        _quickDialog.OpenDialog(actor.PlayerSession, Loc.GetString("nctterminal-title"), Loc.GetString("prayer-popup-notify-pray-ui-message"), (string message) =>
        {
            if (actor.PlayerSession is not null)
            {
                SendNCTDispatch(uid, nameAndJob, message);
                _popupSystem.PopupEntity(Loc.GetString("nctterminal-called"), uid, actor.PlayerSession, PopupType.Large);
                args.Handled = true;
            }
        });
        args.Handled = true;
    }

    public void SendNCTDispatch(EntityUid uid, string nameAndJob, string requestmessage)
    {
        var message = Loc.GetString("nctterminal-message", ("nameAndJob", nameAndJob), ("message", requestmessage));
        var speech = _chat.GetSpeechVerb(uid, message);
        var wrappedMessage = Loc.GetString("chat-radio-message-wrap-bold",
                ("color", "#2681a5"),
                ("languageColor", "#2681a5"),
                ("fontType", speech.FontId),
                ("fontSize", speech.FontSize),
                ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
                ("channel", $"\\[CentComm\\]"),
                ("name", $"[icon src=\"JobIconNanotrasenCareerTrainer\" tooltip=\"NCT Dispatch\"] NCT Dispatch"),
                ("message", message));

        var query = EntityQueryEnumerator<NCTAgentComponent>();
        while (query.MoveNext(out var agent, out var _))
        {
            if (!TryComp<ActorComponent>(agent, out var actor) || actor.PlayerSession is null)
                continue;

            if (_mobState.IsDead(agent))
                continue;

            _chatManager.ChatMessageToOne(ChatChannel.Radio, message, wrappedMessage, uid, false, actor.PlayerSession.Channel);
        }
    }
}
