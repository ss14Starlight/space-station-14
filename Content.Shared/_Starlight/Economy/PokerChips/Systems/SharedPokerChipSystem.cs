using Content.Shared._Starlight.Economy.PokerChips.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;

namespace Content.Shared._Starlight.Economy.PokerChips.Systems;

public abstract partial class SharedPokerChipSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PokerChipComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PokerChipComponent, InteractUsingEvent>(OnInteractUsing);

        InitStack();
    }

    private void OnExamined(Entity<PokerChipComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange) return;

        args.PushMarkup(Loc.GetString(ent.Comp.ExaminedLocId, ("value", Loc.GetString(ent.Comp.ExaminedValueLocId,
            ("value", ent.Comp.ChipValue),
            ("type", ent.Comp.ChipValueType.ToString().ToLower())
        ))));
    }

    private void OnInteractUsing(Entity<PokerChipComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled) return;

        if (TryComp<PokerChipStackComponent>(args.Used, out var stack))
        {
            Insert(ent, (args.Used, stack));
            return;
        }

        if (!TryComp<PokerChipComponent>(args.Used, out var chip))
            return;

        if (!_container.TryGetContainingContainer(args.Used, out var container)) return;
        _container.TryRemoveFromContainer(args.Used);

        var newStack = PredictedSpawnInContainerOrDrop(ent.Comp.StackPrototypeId, container.Owner, container.ID);
        Insert((args.Used, chip), newStack);
        Insert(ent, newStack);

        args.Handled = true;
    }

    protected virtual void ForceAppearanceUpdate(Entity<PokerChipComponent> chip) { }
}
