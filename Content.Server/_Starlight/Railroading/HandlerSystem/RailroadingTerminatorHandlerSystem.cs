using Content.Server._Starlight.Terminator;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Railroading.Events;

namespace Content.Server._Starlight.Railroading;

public sealed partial class RailroadingTerminatorHandlerSystem : EntitySystem
{
    [Dependency] private readonly TerminatorSystem _terminator = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RailroadTerminatorOnChosenComponent, RailroadingCardChosenEvent>(OnCardChosen);
    }

    private void OnCardChosen(EntityUid uid, RailroadTerminatorOnChosenComponent comp, ref RailroadingCardChosenEvent args) => _terminator.CreateTerminator(args.Subject);
}
