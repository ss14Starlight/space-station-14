using System.Linq;
using Content.Server.Cargo.Systems;
using Content.Shared._Starlight.StationGoals;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.StationGoals;

public sealed partial class StationGoalSystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    [Dependency] private IComponentFactory _factory = default!;

    private readonly List<EntityUid> _soldItems = new();

    private HashSet<string>? _foodPieces;

    [SubscribeLocalEvent]
    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            _foodPieces = null;
    }

    private bool IsFoodPiece(EntityUid uid)
    {
        if (MetaData(uid).EntityPrototype?.ID is not { } id)
            return false;

        if (_foodPieces == null)
        {
            _foodPieces = new HashSet<string>();
            foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract
                    || !proto.Components.ContainsKey("Edible")
                    || !proto.TryGetComponent<ToolRefinableComponent>(out var refinable, _factory))
                    continue;

                foreach (var entry in refinable.RefineResult)
                {
                    if (entry.PrototypeId is { } piece)
                        _foodPieces.Add(piece);
                }
            }
        }

        return _foodPieces.Contains(id);
    }

    [SubscribeLocalEvent]
    private void OnEntitySold(ref EntitySoldEvent args)
    {
        if (!TryComp<StationGoalsComponent>(args.Station, out var comp) || comp.Active.Count == 0)
            return;

        _soldItems.Clear();
        foreach (var sold in args.Sold)
            CollectItems(sold);

        var changed = false;
        foreach (var active in comp.Active)
        {
            if (!_proto.Resolve(active.Goal, out var goal))
                continue;

            for (var i = 0; i < goal.Objectives.Count; i++)
            {
                if (goal.Objectives[i] is SellGoalObjective sell)
                    changed |= CountSold(sell, active.Progress[i]);
            }
        }

        if (changed)
            CheckGoals((args.Station, comp));
    }

    private void CollectItems(EntityUid uid)
    {
        _soldItems.Add(uid);

        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
                CollectItems(contained);
        }
    }

    private bool CountSold(SellGoalObjective sell, StationGoalObjectiveProgress progress)
    {
        var changed = false;
        foreach (var item in _soldItems)
        {
            if (!_whitelist.IsValid(sell.Whitelist, item)
                || _whitelist.IsWhitelistPass(sell.Blacklist, item)
                || IsFoodPiece(item))
                continue;

            var units = TryComp<StackComponent>(item, out var stack) ? stack.Count : 1;
            var kind = sell.Kinds > 1 ? MetaData(item).EntityPrototype?.ID ?? string.Empty : string.Empty;
            progress.Kinds[kind] = progress.Kinds.GetValueOrDefault(kind) + units;
            changed = true;
        }

        if (!changed)
            return false;

        progress.Value = sell.Kinds > 1
            ? progress.Kinds.Count(k => k.Value >= sell.Amount)
            : progress.Kinds.GetValueOrDefault(string.Empty);

        return true;
    }
}
