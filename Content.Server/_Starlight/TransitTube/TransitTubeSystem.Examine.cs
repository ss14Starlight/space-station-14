using Content.Shared._Starlight.TransitTube;
using Content.Shared.Disposal.Components;
using Content.Shared.Examine;

namespace Content.Server._Starlight.TransitTube;

public sealed partial class TransitTubeSystem
{
    [SubscribeLocalEvent]
    private void OnTubeExamined(Entity<TransitTubeComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var rotation = Transform(ent).LocalRotation;
        string WorldDir(Direction local) => DirName(new Angle(local.ToAngle() + rotation).GetDir());

        using (args.PushGroup(nameof(TransitTubeComponent)))
        {
            if (ent.Comp.OneWay is { } oneWay)
            {
                if (IsEmagged(ent))
                    args.PushMarkup(Loc.GetString("transit-tube-examine-oneway-broken"));
                else if (!IsEnabled(ent.Owner))
                    args.PushMarkup(Loc.GetString("transit-tube-examine-oneway-off"));
                else
                    args.PushMarkup(Loc.GetString("transit-tube-examine-oneway", ("direction", WorldDir(oneWay))));
            }

            if (TryComp<TransitTubeSwitchComponent>(ent, out var tubeSwitch))
            {
                args.PushMarkup(IsEmagged(ent)
                    ? Loc.GetString("transit-tube-examine-switch-broken")
                    : Loc.GetString("transit-tube-examine-switch",
                        ("side", Loc.GetString(tubeSwitch.Right ? "transit-tube-side-right" : "transit-tube-side-left")),
                        ("direction", WorldDir(tubeSwitch.Right ? tubeSwitch.RightExit : tubeSwitch.LeftExit))));
            }

            if (IsBranchSwitch(ent))
            {
                var branch = WorldDir(ent.Comp.PreferredExit ?? Direction.North);
                if (IsEmagged(ent))
                    args.PushMarkup(Loc.GetString("transit-tube-examine-branch-broken"));
                else if (IsEnabled(ent.Owner))
                    args.PushMarkup(Loc.GetString("transit-tube-examine-branch-on", ("direction", branch)));
                else
                    args.PushMarkup(Loc.GetString("transit-tube-examine-branch-off"));
            }

            if (TryComp<TransitTubeSpeedComponent>(ent, out var speed))
            {
                if (speed.Speed is { } boost)
                {
                    var shown = IsEmagged(ent) ? speed.OverloadSpeed : boost;
                    args.PushMarkup(Loc.GetString("transit-tube-examine-accelerator", ("speed", shown), ("direction", WorldDir(speed.Direction))));
                    if (IsEmagged(ent))
                        args.PushMarkup(Loc.GetString("transit-tube-examine-accelerator-overloaded"));
                }
                else
                {
                    args.PushMarkup(Loc.GetString("transit-tube-examine-brake", ("direction", WorldDir(speed.Direction))));
                }
            }

            if (TryComp<TransitTubeBlockSignalComponent>(ent, out var signal))
                args.PushMarkup(Loc.GetString("transit-tube-examine-signal", ("direction", WorldDir(signal.Direction))));

            if (TryComp<TransitTubeTurntableComponent>(ent, out var turntable))
                args.PushMarkup(Loc.GetString("transit-tube-examine-turntable", ("degrees", Math.Abs(turntable.Turn)), ("clockwise", turntable.Turn < 0)));

            if (TryComp<DisposalTaggerComponent>(ent, out var tagger))
            {
                var tag = tagger.Tag;
                args.PushMarkup(Loc.GetString("transit-tube-examine-tagger", ("tag", string.IsNullOrEmpty(tag) ? "-" : tag)));
            }

            if (TryComp<DisposalRouterComponent>(ent, out var router))
            {
                var tags = router.Tags;
                args.PushMarkup(Loc.GetString("transit-tube-examine-router",
                    ("tags", tags.Count == 0 ? "-" : string.Join(", ", tags)),
                    ("direction", ent.Comp.PreferredExit is { } preferred ? WorldDir(preferred) : "-")));
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnStationExamined(Entity<TransitTubeStationComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(TransitTubeStationComponent)))
        {
            if (GetDestinationName(ent) is { } name)
                args.PushMarkup(Loc.GetString("transit-station-examine-name", ("name", name)));

            if (ent.Comp.RequestStop)
                args.PushMarkup(Loc.GetString(ent.Comp.StopRequested ? "transit-station-examine-requested" : "transit-station-examine-request-stop"));

            if (ent.Comp.Holding)
                args.PushMarkup(Loc.GetString("transit-station-examine-holding"));

            if (ent.Comp.DockedPod is { } podUid && TryComp<TransitPodComponent>(podUid, out var pod))
                PushPodStatus((podUid, pod), args);
            else
                args.PushMarkup(Loc.GetString("transit-station-examine-empty"));
        }
    }

    [SubscribeLocalEvent]
    private void OnPodExamined(Entity<TransitPodComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(TransitPodComponent)))
        {
            PushPodStatus(ent, args);
        }
    }

    private void PushPodStatus(Entity<TransitPodComponent> pod, ExaminedEvent args)
    {
        if (pod.Comp.State == TransitPodState.Open)
        {
            var left = pod.Comp.NextDeparture - _timing.CurTime;
            var parked = TryGetStation(pod.Comp, out var station) && (station.Comp.Holding || station.Comp.Depot) && pod.Comp.Target == null && !pod.Comp.AnyFree;

            args.PushMarkup(parked
                ? Loc.GetString("transit-pod-examine-parked")
                : Loc.GetString("transit-pod-examine-departs", ("seconds", Math.Max(0, (int)Math.Ceiling(left.TotalSeconds)))));
        }

        if (pod.Comp.Target is { } target && TryComp<TransitTubeStationComponent>(target, out var targetStation))
            args.PushMarkup(Loc.GetString("transit-pod-examine-target", ("destination", GetDestinationName((target, targetStation)) ?? "?")));
        else if (pod.Comp.AnyFree)
            args.PushMarkup(Loc.GetString("transit-pod-examine-any"));

        if (pod.Comp.Boosted)
            args.PushMarkup(Loc.GetString("transit-pod-examine-boosted"));
    }

    private string DirName(Direction direction)
        => Loc.GetString($"transit-tube-direction-{direction.ToString().ToLowerInvariant()}");
}
