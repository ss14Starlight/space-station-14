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
    // Starlight start
    // adds a method to let entities change the slope of a radiation source
    public void SetSlope(Entity<RadiationSourceComponent?> entity, float slope)
    {
        // check if the entity has a radiation source component
        if (!SourceQuery.Resolve(entity, ref entity.Comp, false))
            return;

        // check if the slope is actually changing
        if (entity.Comp.Slope == slope)
            return;

        // assign new slope
        entity.Comp.Slope = slope;

        // refresh the radiation source
        UpdateSource((entity, entity.Comp));
    }
    // Starlight end
}
