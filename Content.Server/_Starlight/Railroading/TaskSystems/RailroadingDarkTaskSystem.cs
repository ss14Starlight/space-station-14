using Content.Server._Starlight.Objectives.Events;
using Content.Server._Starlight.Shadekin;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared.Abilities.Goliath;
using Content.Shared.Objectives;
using Content.Shared.Station.Components;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Railroading;

public sealed partial class RailroadDarkTaskSystem : AccUpdateEntitySystem
{
    [Dependency] private readonly RailroadingSystem _railroading = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadDarkTaskComponent, RailroadingCardChosenEvent>(OnTaskPicked);
        SubscribeLocalEvent<RailroadDarkTaskComponent, RailroadingCardCompletionQueryEvent>(OnTaskCompletionQuery);
        SubscribeLocalEvent<RailroadDarkTaskComponent, CollectObjectiveInfoEvent>(OnCollectObjectiveInfo);
    }

    protected override void AccUpdate()
    {
        var query = EntityQueryEnumerator<RailroadDarkTaskComponent>();
        while (query.MoveNext(out var ent, out var comp))
        {
            if (comp.IsCompleted) continue;

            if (comp.Target <= CheckDarkTilesOnStation()
                && TryComp<RailroadCardPerformerComponent>(ent, out var performer)
                && performer.Performer is Entity<RailroadableComponent> railroadable)
            {
                comp.IsCompleted = true;
                _railroading.InvalidateProgress(railroadable);
            }
        }
    }

    public float CheckDarkTilesOnStation()
    {
        var darkTiles = 0;
        var query = EntityQueryEnumerator<DarkTileComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
            if (HasComp<StationMemberComponent>(xform.GridUid))
                darkTiles += 1;

        return darkTiles;
    }

    private void OnCollectObjectiveInfo(Entity<RailroadDarkTaskComponent> ent, ref CollectObjectiveInfoEvent args)
    {
        if (!HasComp<RailroadCardComponent>(ent.Owner))
            return;

        args.Objectives.Add(new ObjectiveInfo
        {
            Title = Loc.GetString(ent.Comp.Message, ("Amount", ent.Comp.Target)),
            Icon = ent.Comp.Icon,
            Progress = Math.Clamp(CheckDarkTilesOnStation() / ent.Comp.Target, 0.0f, 1.0f)
        });
    }

    private void OnTaskCompletionQuery(Entity<RailroadDarkTaskComponent> ent, ref RailroadingCardCompletionQueryEvent args)
    {
        if (args.IsCompleted == false) return;

        args.IsCompleted = ent.Comp.IsCompleted;
    }

    private void OnTaskPicked(Entity<RailroadDarkTaskComponent> ent, ref RailroadingCardChosenEvent args)
    {
        ent.Comp.Target = ent.Comp.Amount.Next(_random);
    }
}
