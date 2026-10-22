using System.Linq;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Actions;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private UserInterfaceSystem _chemistryUi = default!;
    [Dependency] private ActionContainerSystem _actionContainers = default!;

    private static readonly EntProtoId[] ChemicalStingActions = ["ActionChangelingChemistry", "ActionChemicalSting"];

    private void SubscribeChemistry()
    {
        SubscribeLocalEvent<ChangelingComponent, LearnChemicalStingEvent>(OnLearnChemicalSting);
        SubscribeLocalEvent<ChangelingComponent, OpenChangelingChemistryEvent>(OnOpenChemistry);
        SubscribeLocalEvent<ChangelingComponent, ChemicalStingEvent>(OnChemicalSting);
        SubscribeLocalEvent<ChangelingComponent, ChangelingAddReagentMessage>(OnAddStingReagent);
        SubscribeLocalEvent<ChangelingComponent, ChangelingClearMixtureMessage>(OnClearStingMixture);
        SubscribeLocalEvent<ChangelingComponent, ChangelingSetTemperatureMessage>(OnSetStingTemperature);
        SubscribeLocalEvent<ChangelingComponent, ChangelingSetRegenerationMessage>(OnSetStingRegeneration);
        SubscribeLocalEvent<ChangelingComponent, ChangelingSetStingDoseMessage>(OnSetStingDose);
        SubscribeLocalEvent<ChangelingComponent, SolutionChangedEvent>(OnStingSolutionChanged);
    }

    private void OnLearnChemicalSting(EntityUid uid, ChangelingComponent comp, LearnChemicalStingEvent args)
    {
        if (comp.HasChemicalSting)
            return;

        comp.HasChemicalSting = true;
        comp.NextStingRegeneration = _timing.CurTime + TimeSpan.FromSeconds(ChangelingChemistry.RegenerationInterval);
        foreach (var reagent in ChangelingChemistry.BasicReagents)
            comp.StingReagents.Add(reagent, FixedPoint2.Zero);
        InitializeStingChemistry(uid);

        foreach (var action in ChemicalStingActions)
        {
            if (_mind.TryGetMind(uid, out var mind, out _))
                _actionContainers.AddAction(mind, action.Id);
            else
                _actions.AddAction(uid, action);
        }
    }

    private Entity<SolutionComponent> InitializeStingChemistry(EntityUid uid)
    {
        _solution.EnsureSolution(StingContainer(uid), ChangelingChemistry.Solution, out var solution);
        _solution.SetCapacity(solution, ChangelingChemistry.Capacity);
        EnsureComp<UserInterfaceComponent>(uid);
        _chemistryUi.SetUi(uid, ChangelingChemistryUiKey.Key, new InterfaceData("ChangelingChemistryBoundUserInterface"));
        return solution;
    }

    private void OnOpenChemistry(EntityUid uid, ChangelingComponent comp, ref OpenChangelingChemistryEvent args)
    {
        if (args.Handled || !comp.HasChemicalSting)
            return;

        InitializeStingChemistry(uid);
        UpdateStingUi(uid, comp, force: true);
        _chemistryUi.TryToggleUi(uid, ChangelingChemistryUiKey.Key, uid);
        args.Handled = true;
    }

    private void OnChemicalSting(EntityUid uid, ChangelingComponent comp, ref ChemicalStingEvent args)
    {
        if (args.Handled || !comp.HasChemicalSting
            || !_solution.TryGetSolution(StingContainer(uid), ChangelingChemistry.Solution, out var source, out var mixture))
            return;

        if (mixture.Volume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("changeling-chemistry-empty"), uid, uid);
            return;
        }
        if (comp.Chemicals < ChangelingChemistry.StingCost)
        {
            _popup.PopupEntity(Loc.GetString("changeling-chemicals-deficit"), uid, uid);
            return;
        }
        if (!TrySting(uid, args, true)
            || !_solution.TryGetInjectableSolution(args.Target, out var destination, out _))
            return;

        var amount = FixedPoint2.Min(comp.StingDose, mixture.Volume);
        var dose = mixture.Clone().SplitSolution(amount);
        if (!_solution.TryAddSolution(destination.Value, dose))
            return;

        _solution.SplitSolution(source.Value, amount);
        UpdateChemicals(uid, comp, -ChangelingChemistry.StingCost);
        _popup.PopupEntity(Loc.GetString("changeling-sting", ("target", Identity.Entity(args.Target, EntityManager))), uid, uid);
        UpdateStingUi(uid, comp);
        args.Handled = true;
    }

    private void OnAddStingReagent(EntityUid uid, ChangelingComponent comp, ChangelingAddReagentMessage args)
    {
        if (args.Actor != uid || !comp.HasChemicalSting || comp.Biomass < 1
            || args.Amount is < 1 or > ChangelingChemistry.Capacity
            || !comp.StingReagents.TryGetValue(args.Reagent, out var stock)
            || !_solution.TryGetSolution(StingContainer(uid), ChangelingChemistry.Solution, out var solution, out var mixture))
            return;

        var amount = FixedPoint2.Min(args.Amount, stock, mixture.AvailableVolume);
        if (amount <= FixedPoint2.Zero)
            return;

        // Debit before reactions: they may themselves change or destroy the container.
        comp.StingReagents[args.Reagent] -= amount;
        _solution.TryAddReagent(solution.Value, args.Reagent.Id, amount);
        UpdateStingUi(uid, comp);
    }

    private void OnClearStingMixture(EntityUid uid, ChangelingComponent comp, ChangelingClearMixtureMessage args)
    {
        if (args.Actor == uid && comp.HasChemicalSting
            && _solution.TryGetSolution(StingContainer(uid), ChangelingChemistry.Solution, out var solution))
            _solution.RemoveAllSolution(solution.Value);
    }

    private void OnSetStingTemperature(EntityUid uid, ChangelingComponent comp, ChangelingSetTemperatureMessage args)
    {
        if (args.Actor != uid || !comp.HasChemicalSting || !float.IsFinite(args.Temperature))
            return;

        comp.StingTargetTemperature = Math.Clamp(args.Temperature, ChangelingChemistry.MinTemperature, ChangelingChemistry.MaxTemperature);
        UpdateStingUi(uid, comp);
    }

    private void OnSetStingRegeneration(EntityUid uid, ChangelingComponent comp, ChangelingSetRegenerationMessage args)
    {
        if (args.Actor != uid || !comp.HasChemicalSting)
            return;
        comp.StingRegenerationEnabled = args.Enabled;
        UpdateStingUi(uid, comp);
    }

    private void OnSetStingDose(EntityUid uid, ChangelingComponent comp, ChangelingSetStingDoseMessage args)
    {
        if (args.Actor != uid || !comp.HasChemicalSting || args.Dose is not (1 or 5 or 10 or 15 or 20 or 30))
            return;
        comp.StingDose = args.Dose;
        UpdateStingUi(uid, comp);
    }

    private void UpdateStingChemistry(EntityUid uid, ChangelingComponent comp)
    {
        if (!comp.HasChemicalSting)
            return;

        if (comp.Biomass >= 1
            && _solution.TryGetSolution(StingContainer(uid), ChangelingChemistry.Solution, out var solution, out var mixture)
            && mixture.Volume > FixedPoint2.Zero)
        {
            var step = ChangelingChemistry.TemperatureRate * (float) comp.ChemicalUpdateCooldown.TotalSeconds;
            var delta = Math.Clamp(comp.StingTargetTemperature - mixture.Temperature, -step, step);
            if (delta != 0)
                _solution.SetTemperature(solution.Value, mixture.Temperature + delta);
        }

        if (_timing.CurTime >= comp.NextStingRegeneration)
        {
            comp.NextStingRegeneration = _timing.CurTime + TimeSpan.FromSeconds(ChangelingChemistry.RegenerationInterval);
            if (comp.StingRegenerationEnabled && comp.Biomass >= 1)
            {
                var needed = comp.StingReagents.Sum(r => (float) FixedPoint2.Min(1, ChangelingChemistry.ReagentCapacity - r.Value));
                var scale = needed <= 0 ? 0 : Math.Min(1, comp.Chemicals / (needed * ChangelingChemistry.RegenerationCost));
                var spent = FixedPoint2.Zero;
                // Share a limited chemical budget evenly, rather than starving the last reagents.
                foreach (var reagent in ChangelingChemistry.BasicReagents)
                {
                    var amount = FixedPoint2.New((float) FixedPoint2.Min(1, ChangelingChemistry.ReagentCapacity - comp.StingReagents[reagent]) * scale);
                    comp.StingReagents[reagent] += amount;
                    spent += amount;
                }
                if (spent > FixedPoint2.Zero)
                    UpdateChemicals(uid, comp, -(float) spent * ChangelingChemistry.RegenerationCost);
            }
        }
        UpdateStingUi(uid, comp);
    }

    private void OnStingSolutionChanged(EntityUid uid, ChangelingComponent comp, ref SolutionChangedEvent args)
    {
        if (args.Solution.Comp.Id == ChangelingChemistry.Solution)
            UpdateStingUi(uid, comp);
    }

    private void UpdateStingUi(EntityUid uid, ChangelingComponent comp, bool force = false)
    {
        if (!comp.HasChemicalSting || (!force && !_chemistryUi.IsUiOpen(uid, ChangelingChemistryUiKey.Key))
            || !_solution.TryGetSolution(StingContainer(uid), ChangelingChemistry.Solution, out _, out var mixture))
            return;

        _chemistryUi.SetUiState(uid, ChangelingChemistryUiKey.Key, new ChangelingChemistryUiState(
            new(comp.StingReagents), mixture.Clone(), comp.Chemicals, comp.StingTargetTemperature,
            comp.StingRegenerationEnabled, comp.StingDose));
    }

    private void CopyStingChemistry(EntityUid oldBody, EntityUid target, ChangelingComponent source, ChangelingComponent destination)
    {
        if (!source.HasChemicalSting || StingContainer(oldBody) == StingContainer(target))
            return;

        destination.HasChemicalSting = true;
        destination.StingReagents = source.StingReagents;
        destination.NextStingRegeneration = source.NextStingRegeneration;
        destination.StingRegenerationEnabled = source.StingRegenerationEnabled;
        destination.StingTargetTemperature = source.StingTargetTemperature;
        destination.StingDose = source.StingDose;
        var solution = InitializeStingChemistry(target);
        // The old body owns its solution entity; copy its contents before that body is deleted.
        if (_solution.TryGetSolution(StingContainer(oldBody), ChangelingChemistry.Solution, out _, out var mixture))
            _solution.TryAddSolution(solution, mixture);
    }

    private void RemoveStingChemistry(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) || StingContainer(uid) != uid)
            return;
        _chemistryUi.CloseUi(uid, ChangelingChemistryUiKey.Key);
        if (_solution.TryGetSolution(StingContainer(uid), ChangelingChemistry.Solution, out var solution))
            QueueDel(solution.Value.Owner);
    }
}
