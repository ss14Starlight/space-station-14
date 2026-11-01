using Content.Shared.Body.Part;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Body.Systems;

public abstract partial class SharedBodySystem : EntitySystem
{
    /*
     * See the body partial for how this works.
     */

    /// <summary>
    /// Container ID prefix for any body parts.
    /// </summary>
    public const string PartSlotContainerIdPrefix = "body_part_slot_";

    /// <summary>
    /// Container ID for the ContainerSlot on the body entity itself.
    /// </summary>
    public const string BodyRootContainerId = "body_root_part";

    /// <summary>
    /// Container ID prefix for any body organs.
    /// </summary>
    public const string OrganSlotContainerIdPrefix = "body_organ_slot_";

    [Dependency] private   IGameTiming _timing = default!;
    [Dependency] private   IViewVariablesManager _vvm = default!;
    [Dependency] protected IPrototypeManager Prototypes = default!;
    [Dependency] protected DamageableSystem Damageable = default!;
    [Dependency] protected MovementSpeedModifierSystem Movement = default!;
    [Dependency] protected SharedContainerSystem Containers = default!;
    [Dependency] protected SharedTransformSystem SharedTransform = default!;
    [Dependency] protected StandingStateSystem Standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeBody();
        InitializeParts();

        // Debug-only VV paths. The component path supplies the owning entity ID.
        _vvm.GetTypeHandler<BodyPartComponent>()
            .AddPath("BodyPartSlotsVV", (uid, part) =>
            {
                var slots = new List<ContainerSlot>(part.Children.Count);
                foreach (var slotId in part.Children.Keys)
                    slots.Add((ContainerSlot) Containers.GetContainer(uid, GetPartSlotContainerId(slotId)));
                return slots;
            })
            .AddPath("OrganSlotsVV", (uid, part) =>
            {
                var slots = new List<ContainerSlot>(part.Organs.Count);
                foreach (var slotId in part.Organs.Keys)
                    slots.Add((ContainerSlot) Containers.GetContainer(uid, GetOrganContainerId(slotId)));
                return slots;
            });
    }

    public override void Shutdown()
    {
        _vvm.GetTypeHandler<BodyPartComponent>()
            .RemovePath("BodyPartSlotsVV")
            .RemovePath("OrganSlotsVV");

        base.Shutdown();
    }

    /// <summary>
    /// Inverse of <see cref="GetPartSlotContainerId"/>
    /// </summary>
    protected static string? GetPartSlotContainerIdFromContainer(string containerSlotId)
    {
        // This is blursed
        var slotIndex = containerSlotId.IndexOf(PartSlotContainerIdPrefix, StringComparison.Ordinal);

        if (slotIndex < 0)
            return null;

        var slotId = containerSlotId.Remove(slotIndex, PartSlotContainerIdPrefix.Length);
        return slotId;
    }

    /// <summary>
    /// Gets the container Id for the specified slotId.
    /// </summary>
    public static string GetPartSlotContainerId(string slotId)
    {
        return PartSlotContainerIdPrefix + slotId;
    }

    /// <summary>
    /// Gets the container Id for the specified slotId.
    /// </summary>
    public static string GetOrganContainerId(string slotId)
    {
        return OrganSlotContainerIdPrefix + slotId;
    }
}
