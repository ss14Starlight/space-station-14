using System.Linq;
using Content.Server._CD.CartridgeLoader.Cartridges;
using Content.Shared.Access.Components;
using Content.Shared._CD.CartridgeLoader.Cartridges;
using Content.Shared._CD.NanoChat;
using Content.Shared.Delivery;
using Content.Shared.Ghost;
using Content.Shared.PDA;
using Content.Shared._Starlight.Cargo.Mailboxes;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Cargo.Mailboxes;

/// <summary>Queues and delivers batched NanoChat notices for successfully deposited mailbox mail.</summary>
public sealed partial class MailboxNanoChatSystem : EntitySystem
{
    [Dependency] private SharedNanoChatSystem _nanoChat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private const uint MailServiceNumber = 0;
    private static readonly TimeSpan BatchWindow = TimeSpan.FromSeconds(5);
    private TimeSpan _nextFlush = TimeSpan.MaxValue;

    /// <summary>Stores the recipient, pickup mailbox, count, and deadline for one pending notice.</summary>
    private sealed class PendingMessage(string recipientName, string mailboxName, TimeSpan flushAt)
    {
        public string RecipientName = recipientName;
        public string MailboxName = mailboxName;
        public int Count = 1;
        public TimeSpan FlushAt = flushAt;
    }

    private readonly Dictionary<(string RecipientName, string MailboxName), PendingMessage> _pendingMessages = new();

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(NanoChatCartridgeSystem));
        SubscribeLocalEvent<MailBoxComponent, EntInsertedIntoContainerMessage>(OnDeliveryInserted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        FlushDueMessages();
    }

    private void OnDeliveryInserted(Entity<MailBoxComponent> mailbox, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != "mail_storage" || !TryComp<DeliveryComponent>(args.Entity, out var delivery))
            return;

        QueueMessage(delivery.RecipientName, Name(mailbox.Owner));
    }

    private void QueueMessage(string? recipientName, string mailboxName)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
            return;

        var key = (recipientName, mailboxName);
        if (_pendingMessages.TryGetValue(key, out var pending))
        {
            pending.Count++;
            return;
        }

        var flushAt = _timing.CurTime + BatchWindow;
        _pendingMessages.Add(key, new PendingMessage(recipientName, mailboxName, flushAt));
        if (flushAt < _nextFlush)
            _nextFlush = flushAt;
    }

    private void FlushDueMessages()
    {
        if (_pendingMessages.Count == 0 || _timing.CurTime < _nextFlush)
            return;

        var dueMessages = _pendingMessages
            .Where(entry => entry.Value.FlushAt <= _timing.CurTime)
            .ToArray();

        foreach (var (key, pending) in dueMessages)
        {
            _pendingMessages.Remove(key);
            DeliverMessage(pending);
        }

        _nextFlush = TimeSpan.MaxValue;
        foreach (var pending in _pendingMessages.Values)
        {
            if (pending.FlushAt < _nextFlush)
                _nextFlush = pending.FlushAt;
        }
    }

    private void DeliverMessage(PendingMessage pending)
    {
        var query = EntityQueryEnumerator<NanoChatCardComponent, IdCardComponent>();
        while (query.MoveNext(out var cardUid, out var card, out var idCard))
        {
            if (card.Number == null || !string.Equals(idCard.FullName, pending.RecipientName, StringComparison.Ordinal))
                continue;

            if (_nanoChat.GetPdaHolder((cardUid, card)) is not { } holder ||
                !_playerManager.TryGetSessionByEntity(holder, out var session) ||
                session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { } attached ||
                attached != holder ||
                HasComp<GhostComponent>(attached))
                continue;

            var sender = new NanoChatRecipient(MailServiceNumber, Loc.GetString("mailbox-nanochat-sender"));
            _nanoChat.SetRecipient((cardUid, card), MailServiceNumber, sender);

            var message = new NanoChatMessage(
                _timing.CurTime,
                Loc.GetString(pending.Count == 1 ? "mailbox-nanochat-message-one" : "mailbox-nanochat-message-many",
                    ("count", pending.Count),
                    ("mailbox", pending.MailboxName)),
                MailServiceNumber);

            _nanoChat.AddMessage((cardUid, card), MailServiceNumber, message);
            var messageEvent = new NanoChatMessageReceivedEvent(cardUid, message, MailServiceNumber);
            RaiseLocalEvent(ref messageEvent);
        }
    }
}
