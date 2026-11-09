using Content.Shared.Implants.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Implants;

public abstract partial class SharedSubdermalImplantSystem
{
    /// <summary>
    /// Gets the implants currently inside an entity.
    /// </summary>
    /// <param name="uid">The entity to get implants from</param>
    /// <param name="implants">The list of implants found</param>
    /// <returns>True if the entity has implants, false otherwise</returns>
    public bool TryGetImplants(EntityUid uid, out List<EntityUid> implants)
    {
        implants = new List<EntityUid>();
        if (_net.IsClient) return false;

        if (!TryComp<ImplantedComponent>(uid, out var implanted))
            return false;

        var implantContainer = implanted.ImplantContainer;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (implantContainer == null) return false;

        if (implantContainer.ContainedEntities.Count == 0)
            return false;

        implants.AddRange(implantContainer.ContainedEntities);
        return true;
    }
}
