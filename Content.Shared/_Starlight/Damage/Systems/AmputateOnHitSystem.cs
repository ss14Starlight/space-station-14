using Content.Shared._Starlight.Damage.Components;
using Content.Shared.Inventory;
using Content.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Damage.Systems;

/// <summary>
///     Raised before attempting to amputate a limb in combat, so armor can modify
/// </summary>
public sealed class  BeforeAmputateEvent : EntityEventArgs, IInventoryRelayEvent
{
    public float Chance;
    public SlotFlags TargetSlots => ~SlotFlags.POCKET;

    public BeforeAmputateEvent(float chance, EntityUid? origin = null)
    {
        Chance = chance;
    }
}

public partial class SharedAmputateOnHitSystem : EntitySystem
{
    [Dependency] private readonly UseDelaySystem _delay = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public float GetAmputateChance(Entity<AmputateOnHitComponent> weapon, EntityUid target)
    {
        var ev = new BeforeAmputateEvent(weapon.Comp.Chance);
        RaiseLocalEvent(target, ev);
        return ev.Chance;
    }
}
