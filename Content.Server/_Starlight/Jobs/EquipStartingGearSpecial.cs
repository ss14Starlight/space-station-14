using Content.Shared.Roles;
using Content.Shared.Station;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Jobs;

/// <summary>
/// Equips starting gear on jobs using <c>jobEntity</c>, which skip the
/// normal <c>startingGear</c> field.
/// </summary>
[UsedImplicitly]
public sealed partial class EquipStartingGearSpecial : JobSpecial
{
    [DataField(required: true)]
    public ProtoId<StartingGearPrototype> Gear;

    public override void AfterEquip(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var spawning = entMan.System<SharedStationSpawningSystem>();
        spawning.EquipStartingGear(mob, Gear);
    }
}
