using Content.Server.Tools.Innate;
using Content.Shared.Drone;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Storage;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Content.Shared.Mind.Components;
using Content.Shared.Access.Systems;
using System.Linq;

namespace Content.Server.Drone;

public sealed partial class DroneSystem : SharedDroneSystem
{
    [Dependency] private InnateToolSystem _innateToolSystem = default!;
    [Dependency] private AppearanceSystem _appearanceSystem = default!;
    [Dependency] private SharedAccessSystem _access = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DroneComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<DroneComponent, ExaminedEvent>(OnExamined);
        // SubscribeLocalEvent<DroneComponent, EmoteAttemptEvent>(OnEmoteAttempt);
        // SubscribeLocalEvent<DroneComponent, ThrowAttemptEvent>(OnThrowAttempt);
        SubscribeLocalEvent<DroneComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnExamined(EntityUid uid, DroneComponent component, ExaminedEvent args)
        => args.PushMarkup(Loc.GetString("drone-active"));

    private void OnMobStateChanged(EntityUid uid, DroneComponent drone, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            if (TryComp<InnateToolComponent>(uid, out var innate))
                _innateToolSystem.Cleanup(uid, innate);

            if (TryComp<InventoryComponent>(uid, out var inventory))
            {
                var slots = _inventory.GetSlotEnumerator((uid, inventory));
                while (slots.NextItem(out _, out var slot))
                {
                    if (_inventory.TryGetSlotEntity(uid, slot.Name, out var item, inventory))
                        DropStorageContents(item.Value);

                    _inventory.TryUnequip(uid, slot.Name, silent: true, force: true, inventory: inventory);
                }
            }

            QueueDel(uid);
        }
    }

    private void DropStorageContents(EntityUid containerEntity)
    {
        if (!TryComp<StorageComponent>(containerEntity, out var storage))
            return;

        foreach (var item in storage.Container.ContainedEntities.ToArray())
        {
            DropStorageContents(item);
            _container.Remove(item, storage.Container, force: true);
        }
    }

    // private void OnEmoteAttempt(EntityUid uid, DroneComponent component, EmoteAttemptEvent args)
    // {
    //     // Allow screaming with borg sounds, block other emotes
    //     if (args.Emote.ID != "Scream")
    //         args.Cancel();
    // }

    // private void OnThrowAttempt(EntityUid uid, DroneComponent drone, ThrowAttemptEvent args)
    //     => args.Cancel();

    private void OnMindAdded(EntityUid uid, DroneComponent component, MindAddedMessage args)
    {
        UpdateDroneAppearance(uid, DroneStatus.On);
        _access.SetAccessEnabled(uid, true);
    }

    [SubscribeLocalEvent]
    private void OnMindRemoved(EntityUid uid, DroneComponent component, MindRemovedMessage args)
    {
        UpdateDroneAppearance(uid, DroneStatus.Off);
        _access.SetAccessEnabled(uid, false);
    }

    private void UpdateDroneAppearance(EntityUid uid, DroneStatus status)
        => _appearanceSystem.SetData(uid, DroneVisuals.Status, status);
}
