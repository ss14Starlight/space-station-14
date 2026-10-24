using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Radio;
using Content.Server._Starlight.Language;
using Content.Server._Starlight.TextToSpeech;
using Content.Shared._Starlight.Language.Events;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Mind;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.MindCollective;

public sealed partial class MindCollectiveSystem
{
    [Dependency] private IChatManager _collectiveChat = default!;
    [Dependency] private IAdminLogManager _collectiveLog = default!;
    [Dependency] private LanguageSystem _collectiveLanguage = default!;

    [SubscribeLocalEvent]
    private void OnCollectiveMessage(BeforeInGameICMessageEvent args)
    {
        var observer = HasComp<MindCollectiveObserverComponent>(args.Source);
        var prefixed = args.Message.StartsWith("+h ", StringComparison.OrdinalIgnoreCase);
        if (!observer && !prefixed)
            return;
        if (observer && !prefixed && TryComp<MindCollectiveObserverComponent>(args.Source, out var view)
            && view.AllowWorldSpeech)
            return;
        args.Handled |= observer;
        if (!TryGetCollective(args.Source, out var id, out var collective))
            return;
        args.Handled = true;
        if (!TryGetActorMind(args.Source, id, collective, out var mind))
            return;
        var text = (prefixed ? args.Message[3..] : args.Message).Trim();
        if (text.Length == 0)
            return;
        var name = Comp<MindComponent>(mind).CharacterName ?? Name(args.Source);
        var content = FormattedMessage.EscapeText(text);
        var wrapped = Loc.GetString("mind-collective-chat", ("name", FormattedMessage.EscapeText(name)), ("message", content));
        var recipients = GetCommunicationAudience(args.Source, MindCollectiveCommunicationKind.Chat)
            .Select(session => session.Channel).ToList();
        _collectiveChat.ChatMessageToMany(ChatChannel.Radio, content, wrapped, EntityUid.Invalid, false, false, recipients);
        _collectiveLog.Add(LogType.Chat, LogImpact.Low, $"Collective message from {ToPrettyString(args.Source):player}: {text}");
    }

    [SubscribeLocalEvent]
    private void OnCollectiveHearing(ExpandICChatRecipientsEvent args)
    {
        var source = Transform(args.Source);
        var query = EntityQueryEnumerator<MindCollectiveObserverComponent, ActorComponent>();
        while (query.MoveNext(out var observer, out var view, out var actor))
        {
            args.Recipients.Remove(actor.PlayerSession);
            if (!TryGetObserver(observer, out _, out var collective, out _))
                continue;
            EntityUid? nearest = null;
            var distance = args.VoiceRange;
            foreach (var body in collective.Bodies)
            {
                if (!view.Bodies.Contains(body) || !IsRegisteredBody(view.Collective, collective, body))
                    continue;
                var xform = Transform(body);
                if (source.MapID != xform.MapID
                    || !source.Coordinates.TryDistance(EntityManager, xform.Coordinates, out var range) || range > distance)
                    continue;
                nearest = body;
                distance = range;
            }
            if (nearest == null)
                continue;
            view.Listening = nearest;
            _collectiveLanguage.UpdateEntityLanguages(observer);
            args.Recipients[actor.PlayerSession] = new ChatSystem.ICChatRecipientData(distance, false, RemoteListener: nearest);
        }
    }

    [SubscribeLocalEvent]
    private void OnCollectiveLanguages(Entity<MindCollectiveObserverComponent> ent, ref DetermineEntityLanguagesEvent args)
    {
        if (!TryGetObserver(ent, out _, out var collective, out _))
            return;
        if (ent.Comp.Listening is { } body
            && IsRegisteredBody(ent.Comp.Collective, collective, body))
            args.UnderstoodLanguages.UnionWith(_collectiveLanguage.GetUnderstoodLanguages(body));
        if (ent.Comp.AllowWorldSpeech && ent.Comp.Viewing is { } viewed
            && IsRegisteredBody(ent.Comp.Collective, collective, viewed))
            args.SpokenLanguages.UnionWith(_collectiveLanguage.GetSpokenLanguages(viewed));
    }

    [SubscribeLocalEvent]
    private void OnCollectiveTts(ExpandTtsRecipientsEvent args)
    {
        var source = Transform(args.Source);
        var query = EntityQueryEnumerator<MindCollectiveObserverComponent, ActorComponent>();
        while (query.MoveNext(out var observer, out var view, out var actor))
        {
            args.Recipients.RemovePlayer(actor.PlayerSession);
            if (!TryGetObserver(observer, out _, out var collective, out _))
                continue;
            foreach (var body in collective.Bodies)
            {
                if (!view.Bodies.Contains(body) || !IsRegisteredBody(view.Collective, collective, body)
                    || !_collectiveLanguage.CanUnderstand(body, args.Language.ID))
                    continue;
                var xform = Transform(body);
                if (xform.MapID != source.MapID
                    || !source.Coordinates.TryDistance(EntityManager, xform.Coordinates, out var range) || range > args.Range)
                    continue;
                view.Listening = body;
                _collectiveLanguage.UpdateEntityLanguages(observer);
                args.Recipients.AddPlayer(actor.PlayerSession);
                break;
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnCollectiveHeadset(Entity<MindCollectiveBodyComponent> ent, ref HeadsetRadioReceiveRelayEvent args)
        => RelayCollectiveRadio(ent, ent.Comp, args.RelayedEvent);

    [SubscribeLocalEvent]
    private void OnCollectiveRadio(Entity<MindCollectiveBodyComponent> ent, ref RadioReceiveEvent args)
        => RelayCollectiveRadio(ent, ent.Comp, args);

    private void RelayCollectiveRadio(EntityUid body, MindCollectiveBodyComponent member, RadioReceiveEvent message)
    {
        if (!TryComp<MindCollectiveComponent>(member.Collective, out var collective) || collective.Closing
            || !IsRegisteredBody(member.Collective, collective, body))
            return;
        foreach (var observer in collective.Minds.Values)
        {
            if (message.Receivers.Contains(observer) || !TryGetObserver(observer, out var view, out var current, out var actor)
                || current != collective || !view.Bodies.Contains(body))
                continue;
            view.Listening = body;
            _collectiveLanguage.UpdateEntityLanguages(observer);
            var chat = _collectiveLanguage.CanUnderstand(body, message.Language.ID)
                ? message.OriginalChatMsg : message.LanguageObfuscatedChatMsg;
            _collectiveChat.ChatMessageToOne(chat.Channel, chat.Message, chat.WrappedMessage, EntityUid.Invalid,
                false, actor.PlayerSession.Channel);
            message.Receivers.Add(observer);
        }
    }
}
