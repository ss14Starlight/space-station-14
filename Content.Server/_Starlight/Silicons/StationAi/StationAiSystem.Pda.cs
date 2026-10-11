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

    // Create only the program container required by the virtual PDA; replacing the AI's full container manager breaks its other containers.
    [SubscribeLocalEvent]
    private void OnAiHeldInit(Entity<StationAiHeldComponent> ent, ref ComponentInit args) => _container.EnsureContainer<Container>(ent.Owner, SharedCartridgeLoaderSystem.InstalledContainerId);

    // Use the AI entity name on its ID so NanoChat search and messages identify the virtual PDA correctly.
    [SubscribeLocalEvent]
    private void OnStationAiSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!TryComp<StationAiHeldComponent>(args.Mob, out _) ||
            !TryComp<PdaComponent>(args.Mob, out var pda) ||
            pda.ContainedId is not { } idCard)
            return;

        _idCard.TryChangeFullName(idCard, Name(args.Mob));
    }

    // Keep the virtual ID name in sync if the Station AI is renamed after spawning.
    [SubscribeLocalEvent]
    private void OnStationAiRenamed(Entity<StationAiHeldComponent> ent, ref EntityRenamedEvent args)
    {
        if (!TryComp<PdaComponent>(ent.Owner, out var pda) ||
            pda.ContainedId is not { } idCard)
            return;

        _idCard.TryChangeFullName(idCard, args.NewName);
    }

    // Route NanoChat's incoming-message sound only to the player controlling the AI that received it.
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

    // Remove PDA programs before the AI's container manager shuts down, preventing duplicate deletion during teardown.
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

    // Keep the special ID card in the virtual PDA even if a client sends an eject request directly.
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

    // Spawn the configured ID after the PDA slot exists; an already-initialized AI does not populate PDA starting items.
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

        // PresetIdCard sets the title and access but not this prototype ID, which NanoChat identity and ejection checks need.
        idCardComponent.JobPrototype = "StationAi";
        Dirty(idCard, idCardComponent);

        if (_slots.TryInsert(ent.Owner, ent.Comp.IdSlot, idCard, null))
            return;

        Log.Error($"Failed to insert Station AI ID card into PDA {ToPrettyString(ent)}.");
        Del(idCard);
    }
}
