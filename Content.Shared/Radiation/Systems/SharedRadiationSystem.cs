using Content.Shared.Radiation.Components;

namespace Content.Shared.Radiation.Systems;

public abstract partial class SharedRadiationSystem : EntitySystem
{
    [Dependency] protected EntityQuery<RadiationSourceComponent> SourceQuery = default!;
    /// <summary>
    /// Sets the intensity of a <see cref="RadiationSourceComponent"/>.
    /// Does nothing if the entity has no <see cref="RadiationSourceComponent"/>.
    /// </summary>
    /// <param name="entity">Radiation source to update.</param>
    /// <param name="intensity">New intensity value.</param>
    public void SetIntensity(Entity<RadiationSourceComponent?> entity, float intensity)
    {
        if (!SourceQuery.Resolve(entity, ref entity.Comp, false))
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
    # region Starlight
    /// <summary>
    // adds a method to let entities change the slope of a radiation source
    /// Sets the slope of a <see cref="RadiationSourceComponent"/> and refreshes the source.
    /// Does nothing if the entity has no <see cref="RadiationSourceComponent"/> or the slope is unchanged.
    /// </summary>
    /// <param name="entity">Radiation source to update.</param>
    /// <param name="slope">New slope value.</param>
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
    #endregion
}
