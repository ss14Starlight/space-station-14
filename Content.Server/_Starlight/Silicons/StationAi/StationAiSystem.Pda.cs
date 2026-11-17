// ReSharper disable CheckNamespace
using System.Linq;
using Content.Shared.Access.Components;
using Content.Server.Access.Systems;
using Content.Shared._CD.CartridgeLoader.Cartridges;
using Content.Shared._CD.NanoChat;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.CartridgeLoader;
using Content.Shared.GameTicking;
using Content.Shared.PDA;
using Content.Shared.Tag;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Server.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    [Dependency] private IdCardSystem _idCard = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private readonly ProtoId<TagPrototype> _stationAiTag = "StationAi";

    // Use the AI brain name on its ID so NanoChat search and messages identify the PDA across transfers.
    [SubscribeLocalEvent]
    private void OnStationAiSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!_tags.HasTag(args.Mob, _stationAiTag) ||
            !TryComp<PdaComponent>(args.Mob, out var pda) ||
            pda.ContainedId is not { } idCard)
            return;

        _idCard.TryChangeFullName(idCard, Name(args.Mob));
    }

    // Keep the virtual ID name in sync if the Station AI is renamed after spawning.
    [SubscribeLocalEvent]
    private void OnStationAiRenamed(ref EntityRenamedEvent args)
    {
        if (!_tags.HasTag(args.Uid, _stationAiTag) ||
            !TryComp<PdaComponent>(args.Uid, out var pda) ||
            pda.ContainedId is not { } idCard)
            return;

        _idCard.TryChangeFullName(idCard, args.NewName);
    }

    // Route incoming NanoChat sound to the AI's currently inhabited body, not the player holding its card.
    [SubscribeLocalEvent]
    private void OnNanoChatMessageReceived(ref NanoChatMessageReceivedEvent args)
    {
        if (!TryComp<NanoChatCardComponent>(args.CardUid, out var card) ||
            card.NotificationsMuted ||
            !_container.TryGetContainingContainer(args.CardUid, out var pdaContainer) ||
            !HasComp<PdaComponent>(pdaContainer.Owner) ||
            !_tags.HasTag(pdaContainer.Owner, _stationAiTag))
            return;

        var recipient = pdaContainer.Owner;
        if (TryComp<StationAIShuntableComponent>(recipient, out var shuntable) &&
            shuntable.Inhabited is { } inhabited)
            recipient = inhabited;

        if (!_player.TryGetSessionByEntity(recipient, out var session))
            return;

        _audio.PlayGlobal(
            new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg"),
            Filter.SinglePlayer(session),
            false);
    }

    // Detach installed programs before the AI brain's container manager shuts down during entity deletion.
    [SubscribeLocalEvent]
    private void OnStationAiTerminating(ref EntityTerminatingEvent args)
    {
        var brain = args.Entity.Owner;
        if (!_tags.HasTag(brain, _stationAiTag) ||
            !_container.TryGetContainer(brain, SharedCartridgeLoaderSystem.InstalledContainerId, out var programs))
            return;

        foreach (var program in programs.ContainedEntities.ToArray())
        {
            if (_container.Remove(program, programs, reparent: false, force: true))
                continue;

            Log.Error($"Failed to remove Station AI PDA program {ToPrettyString(program)} from terminating brain {ToPrettyString(brain)}.");
        }
    }
}
