using Content.Server.Chat.Managers;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
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

namespace Content.Server._Starlight.Railroading;

public sealed partial class RailroadingDeliveryRewardSystem : EntitySystem
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

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadDeliveryRewardComponent, RailroadingCardChosenEvent>(OnChosen);
        SubscribeLocalEvent<RailroadDeliveryRewardComponent, RailroadingCardCompletedEvent>(OnCompleted);
    }

    private void OnChosen(Entity<RailroadDeliveryRewardComponent> ent, ref RailroadingCardChosenEvent args)
    {
        ent.Comp.RecipientMind = null;
        // Capture recipient identity at card selection time so it does not depend on mutable display names.
        if (_mind.TryGetMind(args.Subject.Owner, out var mindId, out _))
            ent.Comp.RecipientMind = mindId;
    }

    private void OnCompleted(Entity<RailroadDeliveryRewardComponent> ent, ref RailroadingCardCompletedEvent args)
    {
        if (_station.GetStationInMap(Transform(args.Subject).MapID) is not { } station)
            return;

        EntityUid? spawner = null;

        var spawners = EntityQueryEnumerator<DeliverySpawnerComponent>();
        while (spawners.MoveNext(out var spawnerUid, out var spawnerComp))
        {
            var spawnerStation = _station.GetOwningStation(spawnerUid);

            if (spawnerStation != station)
                continue;

            if (!_power.IsPowered(spawnerUid))
                continue;

            spawner = spawnerUid;
            break;
        }

        if (spawner == null)
            return;

        if (ent.Comp.RecipientMind is not { } recipientMind
            || !TryComp<MindComponent>(recipientMind, out var trackedMind)
            || string.IsNullOrWhiteSpace(trackedMind.CharacterName))
            return;

        var delivery = Spawn(ent.Comp.Delivery, Transform(spawner.Value).Coordinates);
        var subjectName = trackedMind.CharacterName!;
        var recordID = _records.GetRecordByName(station, subjectName);

        if (ent.Comp.Dataset != null && _playerManager.TryGetSessionByEntity(args.Subject, out var session))
        {
            var dataset = _protoMan.Index(ent.Comp.Dataset);
            var pick = _random.Pick(dataset.Values);
            if (ent.Comp.WrappedDataset != null)
            {
                var wrappedDataset = _protoMan.Index(ent.Comp.WrappedDataset.Value);
                _chat.ChatMessageToOne(ChatChannel.Notifications, Loc.GetString(pick), Loc.GetString(wrappedDataset.Values[dataset.Values.IndexOf(pick)]), default, false, session.Channel, Color.FromHex("#57A3F7"));
            }
        }

        if (!TryComp<DeliveryComponent>(delivery, out var deliveryComp))
            return;

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
    }
}
