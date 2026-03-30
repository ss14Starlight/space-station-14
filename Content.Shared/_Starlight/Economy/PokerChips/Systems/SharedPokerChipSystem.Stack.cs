using System.Linq;
using Content.Shared._Starlight.Economy.PokerChips.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Economy.PokerChips.Systems;

public abstract partial class SharedPokerChipSystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private void InitStack()
    {
        SubscribeLocalEvent<PokerChipStackComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PokerChipStackComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PokerChipStackComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PokerChipStackComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<PokerChipStackComponent, ExaminedEvent>(OnExamined);
    }

    private void OnStartup(Entity<PokerChipStackComponent> ent, ref ComponentStartup args) =>
        ent.Comp.Container = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);

    private void OnShutdown(Entity<PokerChipStackComponent> ent, ref ComponentShutdown args) =>
        _container.ShutdownContainer(ent.Comp.Container);

    private void OnInteractUsing(Entity<PokerChipStackComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        if (TryComp<PokerChipStackComponent>(args.Used, out var stack))
        {
            TransferByChipCount((args.Used, stack), ent, stack.ChipCount);
            return;
        }

        if (!TryComp<PokerChipComponent>(args.Used, out var chip))
            return;

        Insert((args.Used, chip), ent);

        args.Handled = true;
    }

    private void OnGetAlternativeVerbs(Entity<PokerChipStackComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract || args.Hands is null) return;

        var @event = args;

        var splitCountCategory = new VerbCategory(Loc.GetString(ent.Comp.SplitCountVerbLocId), null);
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.DrawVerbLocId),
            CloseMenu = false,
            Icon = new SpriteSpecifier.Texture(
                new ResPath($"Interface/VerbIcons/{ent.Comp.DrawVerbIconName}.svg.192dpi.png")),
            Act = () => DoSplitVerb(ent, @event)
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.JoinVerbLocId),
            CloseMenu = false,
            Icon = new SpriteSpecifier.Texture(
                new ResPath($"Interface/VerbIcons/{ent.Comp.JoinVerbIconName}.svg.192dpi.png")),
            Act = () =>
            {
            }
        });
        var priority = 0;
        foreach (var amount in ent.Comp.SplitAmounts)
        {
            if (amount < ent.Comp.ChipCount)
                continue;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = amount.ToString("N0"),
                CloseMenu = false,
                Category = splitCountCategory,
                Priority = priority--,
                Act = () => DoSplitVerb(ent, @event, amount)
            });
        }
        // args.Verbs.Add(new AlternativeVerb
        // {
        //     Text = Loc.GetString(ent.Comp.SplitValueVerbLocId),
        //     CloseMenu = false,
        //     Act = () =>
        //     {
        //     }
        // });
    }

    private void OnExamined(Entity<PokerChipStackComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange) return;

        var totalValues = TotalValues(ent);

        var value = string.Join(", ",
            totalValues.Select(v =>
                Loc.GetString(ent.Comp.ExaminedValueLocId, ("value", v.Value), ("type", v.Key.ToString().ToLower()))));

        args.PushMarkup(Loc.GetString(ent.Comp.ExaminedLocId, ("value", value)));
    }

    private void Insert(Entity<PokerChipComponent> toInsert, EntityUid targetStack,
        PokerChipStackComponent? comp = null)
    {
        if (!Resolve(targetStack, ref comp))
            return;

        Insert(toInsert, (targetStack, comp));
    }

    private void Insert(Entity<PokerChipComponent> toInsert, Entity<PokerChipStackComponent> targetStack)
    {
        targetStack.Comp.Chips.Push(GetNetEntity(toInsert));
        var xform = Transform(toInsert);
        _container.Insert((toInsert, xform), targetStack.Comp.Container);
    }

    private void TransferByChipCount(Entity<PokerChipStackComponent> oldStack, Entity<PokerChipStackComponent> newStack, int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            var targetChip = oldStack.Comp.Chips.Pop();
            newStack.Comp.Chips.Push(targetChip);

            var ent = GetEntity(targetChip);
            _container.Insert(ent, newStack.Comp.Container);
        }

        DeleteStackIfEmpty(oldStack);
    }

    private Dictionary<PokerChipValue, int> TotalValues(Entity<PokerChipStackComponent> ent)
    {
        Dictionary<PokerChipValue, int> totalValues = [];
        foreach (var chip in ent.Comp.Chips.Select(GetEntity))
        {
            if (!TryComp<PokerChipComponent>(chip, out var comp))
                return new Dictionary<PokerChipValue, int>();

            if (!totalValues.TryAdd(comp.ChipValueType, comp.ChipValue))
                totalValues[comp.ChipValueType] += comp.ChipValue;
        }

        return totalValues;
    }

    private void DoSplitVerb(Entity<PokerChipStackComponent> ent, GetVerbsEvent<AlternativeVerb> @event, int amount = 1)
    {
        var user = @event.User;

        var held = _hands.GetActiveItem((user, @event.Hands));

        // Transfer top chip into hand stack
        if (TryComp<PokerChipStackComponent>(held, out var stack))
        {
            TransferByChipCount(ent, (held.Value, stack), amount);
            return;
        }

        var targetChip = GetEntity(ent.Comp.Chips.LastOrDefault());
        if (Deleted(targetChip) || EntityManager.IsQueuedForDeletion(targetChip))
            return;
        var xform = Transform(targetChip);
        _container.TryRemoveFromContainer((targetChip, xform));

        if (_hands.ActiveHandIsEmpty((user, @event.Hands)))
        {
            _hands.TryPickupAnyHand(user, targetChip);
            return;
        }

        if (!TryComp<PokerChipComponent>(held, out var chip))
            return;

        // Pull top chip from target stack into hand
        var activeHandId = _hands.GetActiveHand((user, @event.Hands));
        if (activeHandId is null) return; // how the fuck even do you get to here and this is null
        // get hand container directly as to predicted spawn inside the container.
        if (!_container.TryGetContainingContainer(held.Value, out var container)) return;
        _hands.DoDrop((user, @event.Hands), activeHandId);
        var newStack = PredictedSpawnInContainerOrDrop(chip.StackPrototypeId, container.Owner, container.ID);
        Insert((held.Value, chip), newStack);
        DeleteStackIfEmpty(ent);
    }

    private void DeleteStackIfEmpty(Entity<PokerChipStackComponent> stack)
    {
        if (stack.Comp.ChipCount > 1) return;
        {
            if (!stack.Comp.Chips.TryPop(out var nEnt))
                PredictedQueueDel(stack);
            var ent = GetEntity(nEnt);
            _container.TryRemoveFromContainer(ent);
            PredictedQueueDel(stack);
        }
    }
}
