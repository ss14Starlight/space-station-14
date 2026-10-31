using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Examine;

namespace Content.Shared._Starlight.GasMining.GasExtractor;

public abstract partial class SharedGasExtractorSystem : EntitySystem
{
    [Dependency] ISharedAdminLogManager _adminLogger = default!;

    [SubscribeLocalEvent]
    private void OnInit(Entity<GasExtractorComponent> entity, ref ComponentInit args)
    {
        UpdateAppearance(entity);
    }

    protected virtual void UpdateUi(Entity<GasExtractorComponent> entity) {}

    [SubscribeLocalEvent]
    private void OnToggleStatusMessage(Entity<GasExtractorComponent> entity, ref GasExtractorToggleStatusMessage args)
    {
        entity.Comp.Enabled = args.Enabled;
        _adminLogger.Add(LogType.AtmosPowerChanged, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} set the power on {ToPrettyString(entity.Owner):device} to {args.Enabled}");

        Dirty(entity);
        UpdateUi(entity);
        UpdateAppearance(entity);
    }

    [SubscribeLocalEvent]
    private void OnExtractionRateChangeMessage(Entity<GasExtractorComponent> entity, ref GasExtractorChangeExtractionRateMessage args)
    {
        entity.Comp.ExtractionRate = Math.Clamp(args.ExtractionRate, 0f, entity.Comp.MaxExtractionRate);
        Dirty(entity);
        UpdateUi(entity);
        _adminLogger.Add(LogType.AtmosVolumeChanged, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} set the extraction rate on {ToPrettyString(entity.Owner):device} to {args.ExtractionRate}");
    }

    private void OnExamined(Entity<GasExtractorComponent> entity, ref ExaminedEvent args)
    {
        if (!Transform(entity).Anchored)
            return;

        if (Loc.TryGetString("gas-extractor-system-examined",
            out var str,
            ("statusColor", "lightblue"),
            ("rate", entity.Comp.ExtractionRate)
            ))
        {
            args.PushMarkup(str);
        }
    }

    protected void UpdateAppearance(Entity<GasExtractorComponent> entity)
    {

    }

    public void Set(Entity<GasExtractorComponent> entity, bool value)
    {
        if (entity.Comp.Enabled == value)
            return;
        entity.Comp.Enabled = value;
        Dirty(entity);
        UpdateAppearance(entity);
    }
}
