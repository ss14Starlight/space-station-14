using System.Diagnostics.CodeAnalysis;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Labels.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Plumbing.EntitySystems;

public abstract partial class SharedPlumbingSmartDispenserSystem
{
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private OpenableSystem _openable = default!;

    // InjectorSystem draws this much when an injector has no transfer amount set.
    private static readonly FixedPoint2 _injectorDefaultDraw = FixedPoint2.New(5);

    private static readonly SpriteSpecifier _returnVerbIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/insert.svg.192dpi.png"));

    /// <summary>
    /// Whether the user can reach the dispenser while holding something it can fill.
    /// </summary>
    protected bool CanContextDispense(EntityUid user, EntityUid dispenser) =>
        _interaction.InRangeAndAccessible(user, dispenser)
        && _hands.TryGetActiveItem(user, out var held)
        && GetFillSolutionFromContainer(held) is not null;

    [EventSubscription]
    private void OnContextDispense(PlumbingSmartDispenserContextDispenseEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user
            || !Enum.IsDefined(msg.FallbackAmount)
            || !TryGetEntity(msg.Dispenser, out var dispenser)
            || !TryComp<PlumbingSmartDispenserComponent>(dispenser, out var comp)
            || !_interaction.InRangeAndAccessible(user, dispenser.Value))
            return;

        Entity<PlumbingSmartDispenserComponent> ent = (dispenser.Value, comp);

        if (!_hands.TryGetActiveItem(user, out var held) || GetFillSolutionFromContainer(held) is null)
        {
            _popup.PopupClient(Loc.GetString("plumbing-smart-dispenser-no-container"), ent.Owner, user);
            return;
        }

        if (_openable.IsClosed(held.Value, user, predicted: true))
            return;

        TryComp<InjectorComponent>(held, out var injector);
        if (injector?.ReagentWhitelist is { } whitelist && !whitelist.Contains(msg.ReagentId))
        {
            var target = Identity.Entity(ent.Owner, EntityManager);
            _popup.PopupClient(Loc.GetString("injector-component-cannot-draw-message", ("target", target)), ent.Owner, user);
            return;
        }

        var amount = GetContextDispenseAmount(ent, user, held.Value, injector, msg);
        TryDispenseReagent(ent, msg.ReagentId, held.Value, amount, user, true);
        UpdateUi(ent);
    }

    /// <summary>
    /// The held container's transfer amount, or null (fill it) when it is labelled for this reagent
    /// and the dispenser is powered.
    /// </summary>
    private FixedPoint2? GetContextDispenseAmount(
        Entity<PlumbingSmartDispenserComponent> ent,
        EntityUid user,
        EntityUid held,
        InjectorComponent? injector,
        PlumbingSmartDispenserContextDispenseEvent msg)
    {
        if (TryComp<LabelComponent>(held, out var label)
            && !string.IsNullOrWhiteSpace(label.CurrentLabel)
            && TryMatchLabelToReagent(label.CurrentLabel, out var labelledReagent)
            && labelledReagent == msg.ReagentId)
        {
            if (_powerReceiver.IsPowered(ent.Owner))
                return null;

            _popup.PopupClient(Loc.GetString("plumbing-smart-dispenser-not-powered"), ent.Owner, user);
        }

        if (injector != null)
            return injector.CurrentTransferAmount ?? _injectorDefaultDraw;

        if (TryComp<SolutionTransferComponent>(held, out var transfer))
            return transfer.TransferAmount;

        return FixedPoint2.New((int) msg.FallbackAmount);
    }

    [SubscribeLocalEvent]
    private void OnGetInteractionVerbs(
        Entity<PlumbingSmartDispenserComponent> ent,
        ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess
            || !args.CanInteract
            || !args.CanComplexInteract
            || args.Using is not { } held
            || !TryGetReturnableSolution(held, out _, out var solution)
            || solution.Volume <= FixedPoint2.Zero)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("plumbing-smart-dispenser-return-verb"),
            Icon = _returnVerbIcon,
            Act = () => ReturnReagents(ent, user, held),
            Impact = LogImpact.Medium,
        });
    }

    /// <summary>
    /// The held solution that could otherwise be spilled or poured out.
    /// </summary>
    private bool TryGetReturnableSolution(
        EntityUid held,
        [NotNullWhen(true)] out Entity<SolutionComponent>? soln,
        [NotNullWhen(true)] out Solution? solution)
    {
        soln = null;
        solution = null;

        if (_openable.IsClosed(held))
            return false;

        return (TryComp<SpillableComponent>(held, out var spillable)
                && _solutionSystem.TryGetSolution(held, spillable.SolutionName, out soln, out solution))
            || _solutionSystem.TryGetDrainableSolution(held, out soln, out solution);
    }

    private void ReturnReagents(Entity<PlumbingSmartDispenserComponent> ent, EntityUid user, EntityUid held)
    {
        if (!CanInteract(ent, user)
            || !TryGetReturnableSolution(held, out var heldSoln, out var heldSolution)
            || !_solutionSystem.TryGetSolution(ent.Owner,
                ent.Comp.SolutionName,
                out var storeSoln,
                out var storeSolution))
            return;

        var returned = new Solution { Temperature = heldSolution.Temperature };
        var space = storeSolution.AvailableVolume;

        // Same per-reagent cap as plumbing pulls.
        foreach (var reagent in heldSolution.Contents)
        {
            var prototype = reagent.Reagent.Prototype;
            var room = ent.Comp.MaxPerReagent
                - storeSolution.GetTotalPrototypeQuantity(prototype)
                - returned.GetTotalPrototypeQuantity(prototype);
            var amount = FixedPoint2.Min(reagent.Quantity, room, space);
            if (amount <= FixedPoint2.Zero)
                continue;

            returned.AddReagent(reagent.Reagent, amount);
            space -= amount;
        }

        var volume = returned.Volume;
        if (volume <= FixedPoint2.Zero)
        {
            _popup.PopupClient(Loc.GetString("plumbing-smart-dispenser-return-full"), ent.Owner, user);
            return;
        }

        foreach (var reagent in returned.Contents)
        {
            heldSolution.RemoveReagent(reagent);
        }

        _solutionSystem.UpdateChemicals(heldSoln.Value);
        _solutionSystem.TryAddSolution(storeSoln.Value, returned);
        _popup.PopupClient(Loc.GetString("plumbing-smart-dispenser-returned", ("amount", volume)), ent.Owner, user);

        // Same as emptying an injector by spilling it.
        if (heldSolution.Volume == FixedPoint2.Zero
            && TryComp<InjectorComponent>(held, out var injector)
            && _prototypeManager.Resolve(injector.ActiveModeProtoId, out InjectorModePrototype? mode)
            && !mode.Behavior.HasAnyFlag(InjectorBehavior.Draw | InjectorBehavior.Dynamic))
            _injectorSystem.ToggleMode((held, injector), user);

        UpdateUi(ent);
    }
}
