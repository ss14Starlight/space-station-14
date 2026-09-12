using Content.Client._Starlight.HealthAnalyzer.UI;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Mobs.Systems;

namespace Content.Client._Starlight.HealthAnalyzer;

public sealed partial class RotHealthAnalyzerSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = null!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PerishableComponent, CollectHealthAnalyzerVitalsEvent>(OnCollectVitals);
    }

    private void OnCollectVitals(Entity<PerishableComponent> entity, ref CollectHealthAnalyzerVitalsEvent args)
    {
        if (!_mobState.IsDead(entity.Owner))
        {
            return;
        }

        if (entity.Comp.RotAfter <= TimeSpan.Zero)
        {
            return;
        }

        if (TryComp<RottingComponent>(entity.Owner, out var rotting))
        {
            args.Vitals.Add(new HealthAnalyzerVitalsBlockData
            {
                Name = Loc.GetString("starlight-health-analyzer-window-entity-rotted-timer-text"),
                Value = FormatApproximateTime(rotting.TotalRotTime, false),

                HasBar = true,
                BarRatio = 1f,

                ValueColor = Color.Red,
                BarColor = Color.FromHex("#E56F79"),
            });
        }
        else
        {
            var timeUntilRot = entity.Comp.RotAfter - entity.Comp.RotAccumulator;
            var rotRatio = (float)Math.Clamp(
                entity.Comp.RotAccumulator.TotalSeconds / entity.Comp.RotAfter.TotalSeconds,
                0d,
                1d);

            args.Vitals.Add(new HealthAnalyzerVitalsBlockData
            {
                Name = Loc.GetString("starlight-health-analyzer-window-entity-rot-timer-text"),
                Value = FormatApproximateTime(timeUntilRot, true),

                HasBar = true,
                BarRatio = rotRatio,

                ValueColor = Color.White,
                BarColor = Color.FromHex("#D8C560"),
            });
        }
    }

    private string FormatApproximateTime(TimeSpan time, bool countingDown)
    {
        var totalSeconds = Math.Max(0, time.TotalSeconds);

        if (totalSeconds < 60)
            return countingDown
                ? Loc.GetString("starlight-health-analyzer-window-time-imminent")
                : Loc.GetString("starlight-health-analyzer-window-time-justnow");

        var totalMinutes = countingDown
            ? (int)Math.Ceiling(totalSeconds / 60)
            : (int)Math.Floor(totalSeconds / 60);

        return Loc.GetString(
            "starlight-health-analyzer-window-time-minutes",
            ("minutes", totalMinutes));

    }
}
