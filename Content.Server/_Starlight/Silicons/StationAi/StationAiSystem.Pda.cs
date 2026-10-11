using System.Linq;
using Content.Shared.Access.Components;
using Content.Server.Access.Systems;
using Content.Shared._CD.CartridgeLoader.Cartridges;
using Content.Shared._CD.NanoChat;
using Content.Shared.CartridgeLoader;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking;
using Content.Shared.PDA;
using Content.Shared.Silicons.StationAi;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    [Dependency] private IdCardSystem _idCard = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedNanoChatSystem _nanoChat = default!;

    [SubscribeLocalEvent]
    private void OnAiHeldInit(Entity<StationAiHeldComponent> ent, ref ComponentInit args) => _container.EnsureContainer<Container>(ent.Owner, SharedCartridgeLoaderSystem.InstalledContainerId);

    [SubscribeLocalEvent]
    private void OnStationAiSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!TryComp<StationAiHeldComponent>(args.Mob, out _) ||
            !TryComp<PdaComponent>(args.Mob, out var pda) ||
            pda.ContainedId is not { } idCard)
            return;

        _idCard.TryChangeFullName(idCard, Name(args.Mob));
    }

    [SubscribeLocalEvent]
    private void OnStationAiRenamed(Entity<StationAiHeldComponent> ent, ref EntityRenamedEvent args)
    {
        if (!TryComp<PdaComponent>(ent.Owner, out var pda) ||
            pda.ContainedId is not { } idCard)
            return;

        _idCard.TryChangeFullName(idCard, args.NewName);
    }

    [SubscribeLocalEvent]
    private void OnNanoChatMessageReceived(ref NanoChatMessageReceivedEvent args)
    {
        if (!TryComp<NanoChatCardComponent>(args.CardUid, out var card) ||
            card.NotificationsMuted ||
            _nanoChat.GetPdaHolder((args.CardUid, card)) is not { } holder ||
            !HasComp<StationAiHeldComponent>(holder) ||
            !_player.TryGetSessionByEntity(holder, out var session))
            return;

        _audio.PlayGlobal(
            new SoundPathSpecifier("/Audio/Items/bikehorn.ogg"),
            Filter.SinglePlayer(session),
            false);
    }

    [SubscribeLocalEvent]
    private void OnStationAiTerminating(Entity<StationAiHeldComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!_container.TryGetContainer(ent.Owner, SharedCartridgeLoaderSystem.InstalledContainerId, out var programs))
            return;

        foreach (var program in programs.ContainedEntities.ToArray())
        {
            if (_container.Remove(program, programs, reparent: false, force: true))
                continue;

            Log.Error($"Failed to remove Station AI PDA program {ToPrettyString(program)} from terminating entity {ToPrettyString(ent)}.");
        }
    }

    [SubscribeLocalEvent]
    private void OnStationAiIdEjectAttempt(Entity<StationAiHeldComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.SlotEntity == ent.Owner &&
            args.Slot.ID == PdaComponent.PdaIdSlotId &&
            TryComp<IdCardComponent>(args.Item, out var idCard) &&
            idCard.JobPrototype == "StationAi")
        {
            args.Cancelled = true;
        }
    }

    [SubscribeLocalEvent]
    private void OnAiPdaMapInit(Entity<PdaComponent> ent, ref MapInitEvent args)
    {
        if (!HasComp<StationAiHeldComponent>(ent.Owner) ||
            ent.Comp.ContainedId != null ||
            ent.Comp.IdCard is not { } idCardProto)
            return;

        var idCard = Spawn(idCardProto, Transform(ent).Coordinates);
        if (!TryComp<IdCardComponent>(idCard, out var idCardComponent))
        {
            Log.Error($"Station AI ID card prototype {idCardProto} is missing an ID card component.");
            Del(idCard);
            return;
        }

        idCardComponent.JobPrototype = "StationAi";
        Dirty(idCard, idCardComponent);

        if (_slots.TryInsert(ent.Owner, ent.Comp.IdSlot, idCard, null))
            return;

        Log.Error($"Failed to insert Station AI ID card into PDA {ToPrettyString(ent)}.");
        Del(idCard);
    }
}
