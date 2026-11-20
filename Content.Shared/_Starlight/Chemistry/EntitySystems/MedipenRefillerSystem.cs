using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Starlight.Chemistry.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Chemistry.EntitySystems;

/// <summary>
/// Refills empty medipens with their original dose, taken from the containers loaded into the refiller.
/// </summary>
public sealed partial class MedipenRefillerSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> MedipenTag = "Medipen";

    /// <summary>
    /// The dose a fresh pen of this kind comes with, or null if the refiller doesn't take it.
    /// </summary>
    public Solution? GetOriginalFill(Entity<MedipenRefillerComponent> refiller, EntityUid pen)
    {
        if (MetaData(pen).EntityPrototype is not { } penProto
            || !refiller.Comp.Pens.Contains(penProto.ID)
            || !penProto.TryComp<SolutionComponent>(out var solution, EntityManager.ComponentFactory)
            || solution.Solution.Volume <= 0)
        {
            return null;
        }

        return solution.Solution.Clone();
    }

    /// <summary>
    /// Checks whether a pen can be refilled now. On failure, <paramref name="reason"/> is the popup to show.
    /// </summary>
    public bool CanRefill(Entity<MedipenRefillerComponent> refiller, EntityUid pen,
        [NotNullWhen(true)] out Solution? fill, [NotNullWhen(false)] out string? reason)
    {
        fill = null;
        var original = GetOriginalFill(refiller, pen);
        if (original == null || !_solutions.TryGetSolution(pen, refiller.Comp.PenSolution, out _, out var penSolution))
        {
            reason = Loc.GetString("medipen-refiller-unknown-pen");
            return false;
        }

        if (penSolution.Volume > 0)
        {
            reason = Loc.GetString("medipen-refiller-not-empty");
            return false;
        }

        if (!_power.IsPowered(refiller.Owner))
        {
            reason = Loc.GetString("medipen-refiller-unpowered");
            return false;
        }

        var sources = GetSources(refiller);
        foreach (var (reagent, needed) in original.Contents)
        {
            if (refiller.Comp.FreeReagents.Contains(reagent.Prototype))
                continue;

            var held = FixedPoint2.Zero;
            foreach (var source in sources)
            {
                held += source.Comp.Solution.GetTotalPrototypeQuantity(reagent.Prototype);
            }

            if (held >= needed)
                continue;

            string name = _proto.TryIndex<ReagentPrototype>(reagent.Prototype, out var reagentProto)
                ? reagentProto.LocalizedName
                : reagent.Prototype;
            reason = Loc.GetString("medipen-refiller-short", ("reagent", name), ("needed", needed), ("held", held));
            return false;
        }

        fill = original;
        reason = null;
        return true;
    }

    /// <summary>
    /// Takes the pen's dose out of the loaded containers and puts it into the pen. Changes nothing if the pen can't
    /// be refilled.
    /// </summary>
    public bool TryRefill(Entity<MedipenRefillerComponent> refiller, EntityUid pen, out string? reason)
    {
        if (!CanRefill(refiller, pen, out var fill, out reason)
            || !_solutions.TryGetSolution(pen, refiller.Comp.PenSolution, out var penSoln, out _))
        {
            return false;
        }

        var sources = GetSources(refiller);
        foreach (var (reagent, quantity) in fill.Contents)
        {
            if (refiller.Comp.FreeReagents.Contains(reagent.Prototype))
                continue;

            // Each container pays on its own, so nothing from one ends up in another.
            var owed = quantity;
            foreach (var source in sources)
            {
                var solution = source.Comp.Solution;
                foreach (var stack in solution.Contents.ToArray())
                {
                    if (owed <= 0)
                        break;

                    if (stack.Reagent.Prototype == reagent.Prototype)
                        owed -= solution.RemoveReagent(stack.Reagent, FixedPoint2.Min(owed, stack.Quantity));
                }
            }
        }

        foreach (var source in sources)
        {
            _solutions.UpdateChemicals(source, needsReactionsProcessing: false);
        }

        _solutions.TryAddSolution(penSoln.Value, fill);
        return true;
    }

    private List<Entity<SolutionComponent>> GetSources(Entity<MedipenRefillerComponent> refiller)
    {
        var sources = new List<Entity<SolutionComponent>>();
        foreach (var slot in refiller.Comp.Slots)
        {
            if (_itemSlots.GetItemOrNull(refiller, slot) is { } container
                && _solutions.TryGetFitsInDispenser(container, out var soln, out _))
            {
                sources.Add(soln.Value);
            }
        }

        return sources;
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<MedipenRefillerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(MedipenRefillerComponent)))
        {
            var any = false;
            foreach (var slot in ent.Comp.Slots)
            {
                if (_itemSlots.GetItemOrNull(ent, slot) is not { } container)
                    continue;

                any = true;
                var contents = new List<string>();
                if (_solutions.TryGetFitsInDispenser(container, out _, out var solution))
                {
                    foreach (var (reagent, quantity) in solution.Contents)
                    {
                        string name = _proto.TryIndex<ReagentPrototype>(reagent.Prototype, out var reagentProto)
                            ? reagentProto.LocalizedName
                            : reagent.Prototype;
                        contents.Add(Loc.GetString("medipen-refiller-examine-reagent", ("reagent", name), ("amount", quantity)));
                    }
                }

                args.PushMarkup(Loc.GetString("medipen-refiller-examine-container",
                    ("container", container),
                    ("contents", contents.Count > 0
                        ? string.Join(", ", contents)
                        : Loc.GetString("medipen-refiller-examine-container-empty"))));
            }

            if (!any)
                args.PushMarkup(Loc.GetString("medipen-refiller-examine-no-containers"));
        }
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<MedipenRefillerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var listed = MetaData(args.Used).EntityPrototype is { } usedProto && ent.Comp.Pens.Contains(usedProto.ID);
        if (!listed && !_tag.HasTag(args.Used, MedipenTag))
            return;

        args.Handled = true;
        if (!CanRefill(ent, args.Used, out _, out var reason))
        {
            _popup.PopupClient(reason, ent, args.User);
            return;
        }

        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.RefillDelay,
            new MedipenRefillDoAfterEvent(), ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
            BreakOnDropItem = true,
        });

        if (started)
            _appearance.SetData(ent, MedipenRefillerVisuals.Refilling, true);
    }

    [SubscribeLocalEvent]
    private void OnRefillDoAfter(Entity<MedipenRefillerComponent> ent, ref MedipenRefillDoAfterEvent args)
    {
        _appearance.SetData(ent, MedipenRefillerVisuals.Refilling, false);
        if (args.Handled || args.Cancelled || args.Used is not { } pen)
            return;

        args.Handled = true;
        if (!TryRefill(ent, pen, out var reason))
        {
            if (reason != null)
                _popup.PopupClient(reason, ent, args.User);
            return;
        }

        _popup.PopupClient(Loc.GetString("medipen-refiller-done", ("pen", pen)), ent, args.User);
    }
}
