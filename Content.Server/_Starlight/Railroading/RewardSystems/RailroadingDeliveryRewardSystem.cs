using Content.Server.Chat.Managers;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.Abilities.Goliath;
using Content.Shared.Chat;
using Content.Shared.Delivery;
using Content.Shared.FingerprintReader;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Power.EntitySystems;
using Content.Shared.StationRecords;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared._Starlight.Railroading;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Shared.GameTicking;

namespace Content.Server._Starlight.Railroading;

public sealed partial class RailroadingDeliveryRewardSystem : AccUpdateEntitySystem
{
    [Dependency] private readonly FingerprintReaderSystem _fingerprintReader = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly LabelSystem _label = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly StationSystem _station = default!;

    protected override float Threshold { get; set; } = 2.3f;

    private readonly Queue<(Entity<RailroadDeliveryRewardComponent> Card, Entity<RailroadableComponent> Subject)> _queue = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadDeliveryRewardComponent, RailroadingCardChosenEvent>(OnChosen);
        SubscribeLocalEvent<RailroadDeliveryRewardComponent, RailroadingCardCompletedEvent>(OnCompleted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }

    private void OnCleanup(RoundRestartCleanupEvent ev) => _queue.Clear();
    private void OnChosen(Entity<RailroadDeliveryRewardComponent> ent, ref RailroadingCardChosenEvent args)
    {
        ent.Comp.RecipientMind = null;
        // Capture recipient identity at card selection time so it does not depend on mutable display names.
        if (_mind.TryGetMind(args.Subject.Owner, out var mindId, out _))
            ent.Comp.RecipientMind = mindId;
    }

    private void OnCompleted(Entity<RailroadDeliveryRewardComponent> ent, ref RailroadingCardCompletedEvent args) => _queue.Enqueue((ent, args.Subject));

    protected override void AccUpdate(float _)
    {
        var processed = 0;
        while (processed < 5 && _queue.TryDequeue(out var pending))
        {
            if (!TryDeliver(pending.Card, pending.Subject))
                _queue.Enqueue(pending);

            processed++;
        }
    }

    private bool TryDeliver(Entity<RailroadDeliveryRewardComponent> ent, Entity<RailroadableComponent> subject)
    {
        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(subject))
            return true;

        if (_station.GetOwningStation(subject) is not { } station)
            return false;

        EntityUid? spawner = null;

        var spawners = EntityQueryEnumerator<DeliverySpawnerComponent>();
        while (spawners.MoveNext(out var spawnerUid, out var spawnerComp))
        {
            if (_station.GetOwningStation(spawnerUid) is not { } spawnerStation)
                continue;

            if (spawnerStation != station)
                continue;

            if (!_power.IsPowered(spawnerUid))
                continue;

            spawner = spawnerUid;
            break;
        }

        if (spawner == null)
            return false;

        if (ent.Comp.RecipientMind is not { } recipientMind
            || !TryComp<MindComponent>(recipientMind, out var trackedMind)
            || string.IsNullOrWhiteSpace(trackedMind.CharacterName))
            return true;

        var delivery = Spawn(ent.Comp.Delivery, Transform(spawner.Value).Coordinates);
        var subjectName = trackedMind.CharacterName!;
        var recordID = _records.GetRecordByName(station, subjectName);

        if (ent.Comp.Dataset != null && _playerManager.TryGetSessionByEntity(subject, out var session))
        {
            var dataset = _protoMan.Index(ent.Comp.Dataset);
            var pick = _random.Pick(dataset.Values);
            if (ent.Comp.WrappedDataset != null)
            {
                var wrappedDataset = _protoMan.Index(ent.Comp.WrappedDataset.Value);
                _chat.ChatMessageToOne(ChatChannel.Notifications, Loc.GetString(pick), Loc.GetString(wrappedDataset.Values[dataset.Values.IndexOf(pick)]), default, false, session.Channel, Color.FromHex("#FF84FF"));
            }
        }

        if (!TryComp<DeliveryComponent>(delivery, out var deliveryComp))
            return true;

        deliveryComp.RecipientName = subjectName;
        deliveryComp.RecipientStation = station;

        if (recordID != null
            && _records.TryGetRecord<GeneralStationRecord>(_records.Convert((GetNetEntity(station), recordID.Value)), out var entry))
        {
            deliveryComp.RecipientName = entry.Name;
            deliveryComp.RecipientJobTitle = entry.JobTitle;
            _appearance.SetData(delivery, DeliveryVisuals.JobIcon, entry.JobIcon);

            if (TryComp<FingerprintReaderComponent>(delivery, out var reader) && entry.Fingerprint != null)
                _fingerprintReader.AddAllowedFingerprint((delivery, reader), entry.Fingerprint);
        }

        _label.Label(delivery, deliveryComp.RecipientName);

        Dirty(delivery, deliveryComp);
        return true;
    }
}
