using Content.Server.Popups;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Fluids;
using Content.Shared.Interaction;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Virology;

public sealed partial class PathogenContaminationToolSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PathogenContaminationSourceSystem _sources = default!;
    [Dependency] private PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PathogenDecontaminatorComponent, AfterInteractEvent>(OnDecontaminatorUse);
        SubscribeLocalEvent<PathogenSporePatchComponent, InteractUsingEvent>(OnSporePatchInteractUsing);
    }

    private void OnDecontaminatorUse(
        Entity<PathogenDecontaminatorComponent> decontaminator,
        ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (_timing.CurTime < decontaminator.Comp.NextUse)
            return;

        if (!_sources.TrySuppressSource(
                target,
                decontaminator.Comp.SuppressionDuration,
                out _))
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-decontaminator-no-source"),
                decontaminator,
                args.User);
            args.Handled = true;
            return;
        }

        decontaminator.Comp.NextUse = _timing.CurTime + decontaminator.Comp.Cooldown;
        if (HasComp<PathogenSporePatchComponent>(target))
            QueueDel(target);

        _popup.PopupEntity(
            Loc.GetString("pathogen-decontaminator-success", ("target", Name(target))),
            target,
            args.User);
        args.Handled = true;
    }

    private void OnSporePatchInteractUsing(
        Entity<PathogenSporePatchComponent> patch,
        ref InteractUsingEvent args)
    {
        if (!HasComp<AbsorbentComponent>(args.Used))
            return;

        _sources.TrySuppressSource(patch, TimeSpan.Zero, out _);
        QueueDel(patch);
        _popup.PopupEntity(
            Loc.GetString("pathogen-spore-patch-cleaned"),
            patch,
            args.User);
        args.Handled = true;
    }
}
