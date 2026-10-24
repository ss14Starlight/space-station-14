using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Server.Radio;
using Content.Server._Starlight.TextToSpeech;
using Content.Shared._Starlight.Language.Events;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private IChatManager _hiveChat = default!;
    [Dependency] private IAdminLogManager _hiveLog = default!;

    [SubscribeLocalEvent]
    private void OnHiveMessage(BeforeInGameICMessageEvent ev)
    {
        if (!TryGetColony(ev.Source, out _, out var colony))
            return;
        if (!IsHivePlayer(ev.Source, colony))
        {
            ev.Handled |= HasComp<ChangelingMindspaceComponent>(ev.Source);
            return;
        }
        var text = ev.Message;
        var prefixed = text.StartsWith("+r ", StringComparison.OrdinalIgnoreCase);
        if (!HasComp<ChangelingMindspaceComponent>(ev.Source) && !prefixed
            && _language.GetLanguage(ev.Source).ID != "ChangelingHive")
            return;
        ev.Handled = true;
        if (prefixed)
            text = text[3..];
        text = text.Trim();
        if (text.Length == 0 || !_mind.TryGetMind(ev.Source, out _, out var mind))
            return;
        var name = mind.CharacterName ?? Name(ev.Source);
        var content = FormattedMessage.EscapeText(text);
        var wrapped = Loc.GetString("changeling-hive-chat", ("name", FormattedMessage.EscapeText(name)), ("message", content));
        var recipients = colony.Minds.Keys
            .Where(mind => !colony.PendingMinds.Contains(mind))
            .Select(m => TryComp<MindComponent>(m, out var participant) ? participant.UserId : null)
            .Select(id => id is { } user && _player.TryGetSessionById(user, out var session) ? session : null)
            .Where(s => s?.AttachedEntity is { } body
                && (colony.Members.Contains(body) || colony.Minds.Values.Contains(body)))
            .Select(s => s!.Channel).ToList();
        _hiveChat.ChatMessageToMany(ChatChannel.Radio, content, wrapped, EntityUid.Invalid, false, true, recipients);
        _hiveLog.Add(LogType.Chat, LogImpact.Low, $"Hive message from {ToPrettyString(ev.Source):player}: {text}");
    }

    [SubscribeLocalEvent]
    private void OnHiveHearing(ExpandICChatRecipientsEvent ev)
    {
        var source = Transform(ev.Source);
        var query = EntityQueryEnumerator<ChangelingMindspaceComponent, ActorComponent>();
        while (query.MoveNext(out var anchor, out var observer, out var actor))
        {
            if (!TryComp<ChangelingColonyComponent>(observer.Colony, out var colony) || !IsHivePlayer(anchor, colony))
            {
                ev.Recipients.Remove(actor.PlayerSession);
                continue;
            }
            EntityUid? nearest = null;
            var distance = ev.VoiceRange;
            foreach (var body in colony.Members)
            {
                if (TerminatingOrDeleted(body))
                    continue;
                var xform = Transform(body);
                if (source.MapID != xform.MapID
                    || !source.Coordinates.TryDistance(EntityManager, xform.Coordinates, out var range) || range >= distance)
                    continue;
                nearest = body;
                distance = range;
            }
            if (nearest != null)
            {
                observer.Listening = nearest;
                _language.UpdateEntityLanguages(anchor);
                ev.Recipients[actor.PlayerSession] = new ChatSystem.ICChatRecipientData(distance, false, RemoteListener: nearest);
            }
            else
                ev.Recipients.Remove(actor.PlayerSession);
        }
    }

    [SubscribeLocalEvent]
    private void OnHiveLanguages(Entity<ChangelingMindspaceComponent> ent, ref DetermineEntityLanguagesEvent args)
    {
        if (ent.Comp.Listening is { } body && !TerminatingOrDeleted(body))
            args.UnderstoodLanguages.UnionWith(_language.GetUnderstoodLanguages(body));
    }

    [SubscribeLocalEvent]
    private void OnHiveTts(ExpandTtsRecipientsEvent ev)
    {
        var source = Transform(ev.Source);
        var query = EntityQueryEnumerator<ChangelingMindspaceComponent, ActorComponent>();
        while (query.MoveNext(out var anchor, out var observer, out var actor))
        {
            if (!TryComp<ChangelingColonyComponent>(observer.Colony, out var colony) || !IsHivePlayer(anchor, colony))
            {
                ev.Recipients.RemovePlayer(actor.PlayerSession);
                continue;
            }
            foreach (var body in colony.Members)
            {
                if (TerminatingOrDeleted(body) || !_language.CanUnderstand(body, ev.Language.ID))
                    continue;
                var xform = Transform(body);
                if (xform.MapID != source.MapID || !source.Coordinates.TryDistance(EntityManager, xform.Coordinates, out var range)
                    || range > ev.Range)
                    continue;
                observer.Listening = body;
                _language.UpdateEntityLanguages(anchor);
                ev.Recipients.AddPlayer(actor.PlayerSession);
                break;
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnHiveHeadset(Entity<ChangelingColonyMemberComponent> ent, ref HeadsetRadioReceiveRelayEvent args)
        => RelayHiveRadio(ent.Owner, ent.Comp, args.RelayedEvent);

    [SubscribeLocalEvent]
    private void OnHiveRadio(Entity<ChangelingColonyMemberComponent> ent, ref RadioReceiveEvent args)
        => RelayHiveRadio(ent.Owner, ent.Comp, args);

    private void RelayHiveRadio(EntityUid body, ChangelingColonyMemberComponent member, RadioReceiveEvent message)
    {
        if (!TryComp<ChangelingColonyComponent>(member.Mind, out var colony))
            return;
        foreach (var anchor in colony.Minds.Values)
        {
            if (message.Receivers.Contains(anchor) || !TryComp<ActorComponent>(anchor, out var actor) || !IsHivePlayer(anchor, colony))
                continue;
            var chat = _language.CanUnderstand(body, message.Language.ID)
                ? message.OriginalChatMsg : message.LanguageObfuscatedChatMsg;
            _hiveChat.ChatMessageToOne(chat.Channel, chat.Message, chat.WrappedMessage, EntityUid.Invalid,
                false, actor.PlayerSession.Channel);
            message.Receivers.Add(anchor);
        }
    }
}
