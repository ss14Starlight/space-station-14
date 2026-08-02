using System.Linq;
using Content.Server.Popups;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Fluids;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Virology;

public sealed partial class PathogenContaminationToolSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PathogenContaminationSourceSystem _sources = default!;
    [Dependency] private PathogenContaminationSystem _contamination = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PathogenContaminationScannerComponent, UseInHandEvent>(OnScannerUse);
        SubscribeLocalEvent<PathogenDecontaminatorComponent, AfterInteractEvent>(OnDecontaminatorUse);
        SubscribeLocalEvent<PathogenSporePatchComponent, InteractUsingEvent>(OnSporePatchInteractUsing);
    }

    private void OnScannerUse(
        Entity<PathogenContaminationScannerComponent> scanner,
        ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var sourceText = Loc.GetString("pathogen-contamination-source-none");
        if (_sources.TryGetStrongestSource(args.User, out var source))
        {
            var sourceKey = source.BeaconName is null
                ? "pathogen-contamination-source-reading"
                : "pathogen-contamination-source-reading-located";
            sourceText = Loc.GetString(
                sourceKey,
                ("distance", source.Distance.ToString("0")),
                ("direction", source.Direction),
                ("type", FormatSignatureNames(source.PathogenTypes)),
                ("beacon", source.BeaconName ?? string.Empty));
        }

        var groups = new List<PathogenContaminationBeaconGroup>();
        var groupsText = Loc.GetString("pathogen-contamination-beacon-groups-none");
        if (_sources.TryGetBeaconGroups(args.User, out var grid, out groups))
        {
            if (groups.Count > 0)
            {
                groupsText = string.Join(
                    "\n",
                    groups.Select(group => Loc.GetString(
                        "pathogen-contamination-beacon-group",
                        ("beacon", group.BeaconName),
                        ("level", group.Total.ToString("0.0")),
                        ("sources", group.SourceCount),
                        ("infectious", group.InfectiousSourceCount))));
            }

            _ui.SetUiState(
                scanner.Owner,
                PathogenContaminationScannerUiKey.Key,
                new PathogenContaminationScannerUiState(
                    GetNetEntity(grid),
                    Name(grid),
                    groups));
            _ui.TryOpenUi(
                scanner.Owner,
                PathogenContaminationScannerUiKey.Key,
                args.User);
        }

        var dominant = _contamination.GetDominantTypes();
        var dominantText = dominant.Count switch
        {
            0 => Loc.GetString("pathogen-contamination-signature-none"),
            1 => GetSignatureName(dominant[0]),
            _ => Loc.GetString("pathogen-contamination-signature-mixed"),
        };

        var signatures = Loc.GetString(
            "pathogen-contamination-signature-reading",
            ("virus", _contamination.GetContamination(PathogenType.Virus).ToString("0.0")),
            ("bacteria", _contamination.GetContamination(PathogenType.Bacteria).ToString("0.0")),
            ("fungus", _contamination.GetContamination(PathogenType.Fungus).ToString("0.0")),
            ("dominant", dominantText));

        var message = Loc.GetString(
            "pathogen-contamination-scanner-reading",
            ("level", _contamination.Contamination.ToString("0.0")),
            ("signatures", signatures),
            ("sources", _sources.ActiveSourceCount),
            ("source", sourceText),
            ("groups", groupsText));

        _popup.PopupEntity(message, scanner, args.User);
        args.Handled = true;
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

    private string GetSignatureName(PathogenType type)
    {
        var suffix = type.ToString().ToLowerInvariant();
        return Loc.GetString($"pathogen-contamination-signature-{suffix}");
    }

    private string FormatSignatureNames(IReadOnlyList<PathogenType> types)
        => string.Join("/", types.Select(GetSignatureName));
}
