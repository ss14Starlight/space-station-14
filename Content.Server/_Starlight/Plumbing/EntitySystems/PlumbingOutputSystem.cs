using Content.Server._Starlight.Plumbing.Components;
using Content.Server.Popups;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using JetBrains.Annotations;


namespace Content.Server._Starlight.Plumbing.EntitySystems;

/// <summary>
///     Handles plumbing output machine behavior: A small tank that players can draw reagents from.
///     Pulling from the network is handled by <see cref="PlumbingInletSystem"/> via <see cref="PlumbingInletComponent"/>.
/// </summary>
[UsedImplicitly]
public sealed class PlumbingOutputSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlumbingOutputComponent, InteractUsingEvent>(OnOutputInteractUsing);
    }

    private void OnOutputInteractUsing(Entity<PlumbingOutputComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_solutionSystem.TryGetRefillableSolution(args.Used, out var refillableSolutionEnt, out var refillableSolution))
            return;

        if (!_solutionSystem.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out var outputSolutionEnt, out var outputSolution))
            return;

        var transferAmount = outputSolution.Volume;
        if (TryComp<SolutionTransferComponent>(args.Used, out var transferComp))
            transferAmount = FixedPoint2.Min(transferAmount, transferComp.TransferAmount);

        var space = refillableSolution.AvailableVolume;
        var toTransfer = FixedPoint2.Min(transferAmount, space);

        if (toTransfer <= 0)
        {
            _popup.PopupEntity(Loc.GetString("plumbing-output-empty"), ent.Owner, args.User);
            return;
        }

        var split = _solutionSystem.SplitSolution(outputSolutionEnt.Value, toTransfer);
        _solutionSystem.TryAddSolution(refillableSolutionEnt.Value, split);

        _popup.PopupEntity(Loc.GetString("plumbing-output-filled", ("amount", toTransfer)), ent.Owner, args.User);

        args.Handled = true;
    }
}
