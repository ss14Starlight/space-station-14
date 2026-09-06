using Content.Server.Nuke;
using Content.Shared._Starlight.DestinyDice;
using Content.Shared._Starlight.DestinyDice.Effects;
using Content.Shared.EntityEffects;
using Content.Shared.Nuke;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server._Starlight.DestinyDice.EffectSystems;

public sealed partial class NukeArmEffectSystem : EntityEffectSystem<DestinyDiceComponent, NukeArmEffect>
{
    [Dependency] private NukeSystem _nuke = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private IRobustRandom _random = default!;

    protected override void Effect(Entity<DestinyDiceComponent> entity, ref EntityEffectEvent<NukeArmEffect> args)
    {
        var query = EntityQueryEnumerator<NukeDiskComponent>();
        List<EntityUid> disks = []; // Pick randomly in the event that multiple disks are found + none on station grid.
        while (query.MoveNext(out var uid, out _))
        {
            if (Transform(uid).GridUid == entity.Comp.ActiveGrid)
            {
                Arm(uid);
                return;
            }
            disks.Add(uid);
        }

        if (disks.Count == 0) return;
        Arm(_random.Pick(disks));
    }

    private void Arm(EntityUid disk)
    {
        var query = EntityQueryEnumerator<NukeComponent>();
        while (query.MoveNext(out var uid, out _))
            if (Transform(uid).GridUid == Transform(disk).GridUid)
            {
                _xform.AnchorEntity(uid); // force anchor it
                _container.Insert(disk, _container.GetContainer(uid, "Nuke"));
                _nuke.ArmBomb(uid);
                return;
            }
    }
}
