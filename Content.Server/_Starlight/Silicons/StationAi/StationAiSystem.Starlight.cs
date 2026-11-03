using Content.Server.Chat.Systems;
using Content.Server._Starlight.Station.Systems;
using Content.Server._Starlight.Utility;
using Content.Server._Starlight.Utility.Events;
using Content.Server.Pinpointer;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.Chat.Prototypes;
using Content.Shared._Starlight.Mind.Events;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Power.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

// ReSharper disable CheckNamespace
namespace Content.Server.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DelayedEventSystem _delayedEvent = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private ContainerSpawnJobSlotSystem _containerSpawnJobSlots = default!;

    private const string AiPowerLossAlertEventId = "station-ai-power-loss-alert";
    private static readonly TimeSpan _aiPowerLossAlertDelay = TimeSpan.FromSeconds(3);

    public override void Shutdown()
    {
        base.Shutdown();
        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        if (_mind.TryGetMind(args.Session.UserId, out _, out var mind)
            && mind.OwnedEntity is { } body)
        {
            if (!EjectContainedAi(body)
                && _tags.HasTag(body, _stationAiTag)
                && _container.TryGetContainingContainer(body, out var container)
                && container.ID == StationAiHolderComponent.Container
                && TryComp<StationAiHolderComponent>(container.Owner, out _))
                return;
        }

        FreeStationAiJobSlot(args.Session.UserId);
    }

    [SubscribeLocalEvent]
    private void OnMindRemoved(Entity<MindContainerComponent> entity, ref MindRemovedMessage args)
    {
        var wasInAiHolder = TryGetStationAiHolder(entity.Owner, out var holder);
        var wasInAiCore = wasInAiHolder && HasComp<StationAiCoreComponent>(holder);
        var inCryostorage = HasComp<CryostorageContainedComponent>(entity.Owner);
        var permanentlyGhostedFromAiHolder = args.TransferEntity is { } ghostTarget
            && TryComp<GhostComponent>(ghostTarget, out var holderGhost)
            && !holderGhost.CanReturnToBody
            && wasInAiHolder;
        var permanentlyGhostedFromCore = permanentlyGhostedFromAiHolder && wasInAiCore;

        if (!EjectContainedAi(entity.Owner) && !permanentlyGhostedFromCore)
            return;

        FreeStationAiJobSlot(args.Mind.Comp.UserId);

        if (!wasInAiHolder || inCryostorage)
            return;

        if (permanentlyGhostedFromAiHolder)
        {
            AnnounceStationAiStatus(entity.Owner, holder, "station-ai-deathrattle-shutdown");
            return;
        }

        if (!TryComp<MobStateComponent>(entity.Owner, out var mobState)
            || mobState.CurrentState == MobState.Alive)
            AnnounceStationAiStatus(entity.Owner, holder, "station-ai-deathrattle-connection-lost");
    }

    [SubscribeLocalEvent]
    private void OnMindUserIdChanged(Entity<MindContainerComponent> entity, ref MindUserIdChangedEvent args)
    {
        if (args.NewUserId != null)
            return;

        if (EjectContainedAi(entity.Owner))
            FreeStationAiJobSlot(args.OldUserId);
    }

    private void FreeStationAiJobSlot(NetUserId? userId)
    {
        if (userId is not { } playerId)
            return;

        var slotReturned = false;

        foreach (var station in _station.GetStationsSet())
        {
            if (!_stationJobs.TryGetPlayerJobs(station, playerId, out var jobs)
                || !jobs.Contains(_stationAiJob))
                continue;

            if (!_stationJobs.TryRemovePlayerJob(station, playerId, _stationAiJob))
            {
                Log.Error($"Failed to remove Station AI job assignment for player {playerId} from {ToPrettyString(station)}.");
                continue;
            }

            if (!_stationJobs.TryAdjustJobSlot(station, _stationAiJob, 1, clamp: true))
            {
                Log.Error($"Failed to restore Station AI job slot for player {playerId} on {ToPrettyString(station)}.");
                continue;
            }

            slotReturned = true;
        }

        if (slotReturned)
            _containerSpawnJobSlots.RefreshJobSlots();
    }

    private bool EjectContainedAi(EntityUid body)
    {
        if (!_tags.HasTag(body, _stationAiTag)
            || !_container.TryGetContainingContainer(body, out var container)
            || container.ID != StationAiHolderComponent.Container
            || !TryComp<StationAiHolderComponent>(container.Owner, out var holder))
            return false;

        if (_slots.TryEject(container.Owner, holder.Slot, null, out _))
            return true;

        Log.Error(
            $"Failed to eject AI {ToPrettyString(body)} from holder {ToPrettyString(container.Owner)} during mind lifecycle cleanup.");
        return false;
    }

    [SubscribeLocalEvent]
    private void OnDelayedEventTriggered(Entity<StationAiCoreComponent> ent, ref DelayedEventTriggeredEvent args)
    {
        if (args.EventId != AiPowerLossAlertEventId
            || !HasComp<ApcPowerReceiverBatteryComponent>(ent)
            || !TryGetHeld((ent.Owner, ent.Comp), out var held))
            return;

        var ev = new ChatNotificationEvent(_aiLosingPowerChatNotificationPrototype, ent.Owner);
        RaiseLocalEvent(held.Value, ref ev);
    }

    private void AnnounceAiOffline(EntityUid ai)
    {
        if (HasComp<CryostorageContainedComponent>(ai)
            || !TryGetStationAiCore(ai, out var core))
            return;

        AnnounceStationAiStatus(ai, core, "station-ai-deathrattle-connection-lost");
    }

    private void AnnounceAiCoreConnectionLost(EntityUid ai, EntityUid core)
    {
        if (HasComp<CryostorageContainedComponent>(ai)
            || !HasComp<StationAiCustomizationComponent>(ai)
            || !_mind.TryGetMind(ai, out _, out _)
            || (TryComp<MobStateComponent>(ai, out var mobState) && mobState.CurrentState != MobState.Alive))
            return;

        AnnounceStationAiStatus(ai, core, "station-ai-deathrattle-connection-lost");
    }

    private bool TryGetStationAiCore(EntityUid ai, out EntityUid core)
    {
        core = default;

        if (!_container.TryGetContainingContainer(ai, out var container)
            || container.ID != StationAiCoreComponent.Container
            || !HasComp<StationAiCoreComponent>(container.Owner))
            return false;

        core = container.Owner;
        return true;
    }

    private bool TryGetStationAiHolder(EntityUid ai, out EntityUid holder)
    {
        holder = default;

        if (!_container.TryGetContainingContainer(ai, out var container)
            || container.ID != StationAiHolderComponent.Container
            || !HasComp<StationAiHolderComponent>(container.Owner))
            return false;

        holder = container.Owner;
        return true;
    }

    private void AnnounceStationAiStatus(EntityUid ai, EntityUid source, string messageId)
    {
        var aiName = FormattedMessage.RemoveMarkupOrThrow(MetaData(ai).EntityName);
        var position = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(source));
        var message = Loc.GetString(messageId, ("ai", aiName), ("position", position));
        var sender = Loc.GetString("station-ai-deathrattle-sender");

        _chat.DispatchStationAnnouncement(source, message, sender, playDefaultSound: false);
    }
}
