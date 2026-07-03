using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Shared._Starlight.Railroading.Components.Handlers;
using Content.Shared._Starlight.Railroading.Events;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Railroading.HandlerSystem;

public sealed partial class RailroadingAnnounceHandlerSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadAnnounceOnChosenComponent, RailroadingCardChosenEvent>(OnChosen);
    }

    private void OnChosen(Entity<RailroadAnnounceOnChosenComponent> ent, ref RailroadingCardChosenEvent args)
        => _chatSystem.DispatchFilteredAnnouncement
        (
            Filter.Empty().AddWhere(_gameTicker.UserHasJoinedGame),
            Loc.GetString(_random.Pick(ent.Comp.Text)),
            playSound: ent.Comp.PlaySound,
            announcementSound: ent.Comp.Sound,
            colorOverride: ent.Comp.Color
        );
}
