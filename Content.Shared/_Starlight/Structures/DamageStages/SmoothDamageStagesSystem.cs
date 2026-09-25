using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;

namespace Content.Shared._Starlight.Structures.DamageStages;

public sealed partial class SmoothDamageStagesSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<SmoothDamageStagesComponent> ent, ref MapInitEvent args)
        => UpdateStage(ent);

    [SubscribeLocalEvent]
    private void OnDamageChanged(Entity<SmoothDamageStagesComponent> ent, ref DamageChangedEvent args)
        => UpdateStage(ent);

    private void UpdateStage(Entity<SmoothDamageStagesComponent> ent)
    {
        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return;

        var stage = 0;
        foreach (var threshold in ent.Comp.Thresholds)
        {
            if (damageable.TotalDamage < threshold)
                break;

            stage++;
        }

        _appearance.SetData(ent, SmoothDamageStageVisuals.Stage, stage);
    }
}
