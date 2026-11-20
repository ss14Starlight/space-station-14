using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Starlight.Chemistry.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Chemistry.EntitySystems;

/// <summary>
/// Refills empty medipens with their original dose, taken from the refiller's tank.
/// </summary>
public sealed partial class MedipenRefillerSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
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

        _solutions.TryGetSolution(refiller.Owner, refiller.Comp.Solution, out _, out var tank);
        foreach (var (reagent, needed) in original.Contents)
        {
            if (refiller.Comp.FreeReagents.Contains(reagent.Prototype))
                continue;

            var held = tank?.GetTotalPrototypeQuantity(reagent.Prototype) ?? FixedPoint2.Zero;
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
    /// Takes the pen's dose out of the tank and puts it into the pen. Changes nothing if the pen can't be refilled.
    /// </summary>
    public bool TryRefill(Entity<MedipenRefillerComponent> refiller, EntityUid pen, out string? reason)
    {
        if (!CanRefill(refiller, pen, out var fill, out reason)
            || !_solutions.TryGetSolution(refiller.Owner, refiller.Comp.Solution, out var tankSoln, out var tank)
            || !_solutions.TryGetSolution(pen, refiller.Comp.PenSolution, out var penSoln, out _))
        {
            return false;
        }

        foreach (var (reagent, quantity) in fill.Contents)
        {
            if (refiller.Comp.FreeReagents.Contains(reagent.Prototype))
                continue;

            // Any stack of the reagent pays, whatever data it carries. Going stack by stack keeps the amount exact.
            var owed = quantity;
            foreach (var stack in tank.Contents.ToArray())
            {
                if (owed <= 0)
                    break;

                if (stack.Reagent.Prototype == reagent.Prototype)
                    owed -= tank.RemoveReagent(stack.Reagent, FixedPoint2.Min(owed, stack.Quantity));
            }
        }

        _solutions.UpdateChemicals(tankSoln.Value, needsReactionsProcessing: false);
        _solutions.TryAddSolution(penSoln.Value, fill);
        return true;
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
