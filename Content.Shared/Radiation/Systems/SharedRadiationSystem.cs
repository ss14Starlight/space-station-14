using Content.Shared.Radiation.Components;

namespace Content.Shared.Radiation.Systems;

public abstract partial class SharedRadiationSystem : EntitySystem
{
    [Dependency] protected EntityQuery<RadiationSourceComponent> SourceQuery = default!;

    public void SetIntensity(Entity<RadiationSourceComponent?> entity, float intensity)
    {
        if (!SourceQuery.Resolve(entity, ref entity.Comp))
            return;

        entity.Comp.Intensity = intensity;
        UpdateSource((entity, entity.Comp));
    }

    protected virtual void UpdateSource(Entity<RadiationSourceComponent> entity)
    {

    }

    /// <summary>
    /// Change intensity of a <see cref="RadiationSourceComponent"/> using the passed intensity.
    /// </summary>
    /// <param name="ent">Radiation source we're attempting to update. </param>
    /// <param name="intensityChange">Intensity change value that will be added to current intensity value.</param>
    public void ChangeIntensity(Entity<RadiationSourceComponent?> ent, float intensityChange)
    {
        if (!SourceQuery.Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.Intensity = MathF.Max(0f, ent.Comp.Intensity + intensityChange);
    }
}
