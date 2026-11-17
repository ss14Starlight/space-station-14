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

    private const uint MailServiceNumber = 0; // Groups all automated mailbox notices in one NanoChat conversation.
    private static readonly TimeSpan BatchWindow = TimeSpan.FromSeconds(5); // Gives several parcels time to combine into one notice.
    private TimeSpan _nextFlush = TimeSpan.MaxValue; // Lets Update skip checking every batch until one could be due.

    /// <summary>Stores the recipient, pickup mailbox, count, and deadline for one pending notice.</summary>
    private sealed class PendingMessage(string recipientName, string mailboxName, TimeSpan flushAt)
    {
        public string RecipientName = recipientName; // Used later to find the recipient's ID and NanoChat card.
        public string MailboxName = mailboxName; // Included in the message so the recipient knows where to collect mail.
        public int Count = 1; // The first deposited parcel creates this batch.
        public TimeSpan FlushAt = flushAt; // Keep the first parcel's deadline fixed as more parcels arrive.
    }

    private readonly Dictionary<(string RecipientName, string MailboxName), PendingMessage> _pendingMessages = new(); // A separate key keeps different people and mailboxes in separate notices.

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(NanoChatCartridgeSystem)); // Flush after NanoChat refreshes each cartridge's current card reference.
        SubscribeLocalEvent<MailBoxComponent, EntInsertedIntoContainerMessage>(OnDeliveryInserted); // Listen only to mailboxes after an item is accepted.
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        FlushDueMessages(); // Send batches whose five-second collection window has expired.
    }

    private void OnDeliveryInserted(Entity<MailBoxComponent> mailbox, ref EntInsertedIntoContainerMessage args)
    {
        // Mailboxes have other containers too; only mail-storage insertions represent deposited parcels.
        if (args.Container.ID != "mail_storage" || !TryComp<DeliveryComponent>(args.Entity, out var delivery))
            return;

        QueueMessage(delivery.RecipientName, Name(mailbox.Owner)); // Use the localized mailbox type as the pickup location.
    }

    private void QueueMessage(string? recipientName, string mailboxName)
    {
        if (string.IsNullOrWhiteSpace(recipientName)) // Without a name, the intended NanoChat card cannot be found.
            return;

        var key = (recipientName, mailboxName); // Parcels for the same person and mailbox share a single notice.
        if (_pendingMessages.TryGetValue(key, out var pending))
        {
            pending.Count++; // Include this parcel without extending the existing batch's deadline.
            return;
        }

        var flushAt = _timing.CurTime + BatchWindow; // Start a fixed window when the first parcel arrives.
        _pendingMessages.Add(key, new PendingMessage(recipientName, mailboxName, flushAt)); // Store only message details, not the parcel entity.
        if (flushAt < _nextFlush)
            _nextFlush = flushAt; // Track the nearest deadline so Update can avoid unnecessary scans.
    }

    private void FlushDueMessages()
    {
        if (_pendingMessages.Count == 0 || _timing.CurTime < _nextFlush) // Skip work while no batch can be ready.
            return;

        var dueMessages = _pendingMessages
            .Where(entry => entry.Value.FlushAt <= _timing.CurTime)
            .ToArray(); // Snapshot entries because delivery removes them from the dictionary.

        foreach (var (key, pending) in dueMessages)
        {
            _pendingMessages.Remove(key); // Remove first so this batch cannot be sent again next update.
            DeliverMessage(pending); // Send one message containing the batch's parcel count.
        }

        _nextFlush = TimeSpan.MaxValue; // Reset the deadline before finding the next pending batch.
        foreach (var pending in _pendingMessages.Values)
        {
            if (pending.FlushAt < _nextFlush)
                _nextFlush = pending.FlushAt; // Remember the soonest remaining batch deadline.
        }
    }

    private void DeliverMessage(PendingMessage pending)
    {
        var query = EntityQueryEnumerator<NanoChatCardComponent, IdCardComponent>(); // Match each NanoChat card to its owner's ID-card name.
        while (query.MoveNext(out var cardUid, out var card, out var idCard))
        {
            if (card.Number == null || !string.Equals(idCard.FullName, pending.RecipientName, StringComparison.Ordinal)) // Ignore unnumbered cards and cards belonging to someone else.
                continue;

            // Require an online player holding this PDA; dead bodies remain eligible, but ghost entities do not.
            if (_nanoChat.GetPdaHolder((cardUid, card)) is not { } holder ||
                !_playerManager.TryGetSessionByEntity(holder, out var session) ||
                session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { } attached ||
                attached != holder ||
                HasComp<GhostComponent>(attached))
                continue;

            var sender = new NanoChatRecipient(MailServiceNumber, Loc.GetString("mailbox-nanochat-sender")); // Give the automated message its localized sender name.
            _nanoChat.SetRecipient((cardUid, card), MailServiceNumber, sender); // Ensure the Mail Service conversation exists on this card.

            var message = new NanoChatMessage(
                _timing.CurTime, // Record when the server delivered this notice.
                Loc.GetString(pending.Count == 1 ? "mailbox-nanochat-message-one" : "mailbox-nanochat-message-many",
                    ("count", pending.Count),
                    ("mailbox", pending.MailboxName)), // Localize count and location; never include parcel contents.
                MailServiceNumber); // Associate the message with the Mail Service conversation.

            _nanoChat.AddMessage((cardUid, card), MailServiceNumber, message); // Keep the message even when the player has muted notifications.
            var messageEvent = new NanoChatMessageReceivedEvent(cardUid, message, MailServiceNumber); // Build NanoChat's normal incoming-message event.
            RaiseLocalEvent(ref messageEvent); // Let NanoChat update unread state, UI, and mute-aware alerts.
        }
    }
}
