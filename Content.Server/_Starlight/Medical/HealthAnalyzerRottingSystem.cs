using Content.Shared._Starlight.Medical.HealthAnalyzer;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Starlight.Medical;

public sealed partial class RotHealthAnalyzerSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = null!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PerishableComponent, CollectHealthAnalyzerExtensionsEvent>(OnCollectVitals);
    }

    private void OnCollectVitals(Entity<PerishableComponent> entity, ref CollectHealthAnalyzerExtensionsEvent args)
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
                Name = Loc.GetString("starlight-health-analyzer-window-entity-rot-timer-text"),
                Value = FormatApproximateTime(rotting.TotalRotTime, false),

                HasBar = true,
                BarRatio = 1f,

                ValueColor = Color.Red,
                BarColor = HealthAnalyzerFormatting.GetDamageSeverityColorUi(1f),
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
                BarColor = HealthAnalyzerFormatting.GetDamageSeverityColorUi(rotRatio),
            });
        }
    }

    private string FormatApproximateTime(TimeSpan time, bool countingDown)
    {
        var totalSeconds = Math.Max(0, time.TotalSeconds);
        var totalMinutes = (int)Math.Round(totalSeconds / 60);

        if (countingDown)
        {
            if (totalMinutes <= 1)
                return Loc.GetString("starlight-health-analyzer-window-time-under-minute");


            return Loc.GetString("starlight-health-analyzer-window-time-in-minutes", ("minutes", totalMinutes));
        }
        else
        {
            if (totalMinutes <= 1)
                Loc.GetString("starlight-health-analyzer-window-time-justnow");

            return Loc.GetString("starlight-health-analyzer-window-time-since-minutes", ("minutes", totalMinutes));
        }



    }
}
