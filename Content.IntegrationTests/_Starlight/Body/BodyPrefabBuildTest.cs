// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.PartDamage;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._Starlight.Body;

[TestFixture]
[TestOf(typeof(BodyPrefabPrototype))]
public sealed class BodyPrefabBuildTest : GameTest
{
    [Test]
    public async Task AllPrefabsBuild()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var bodySys = entMan.System<SLBodySystem>();
        var map = await Pair.CreateTestMap();

        bool IsAnimalPart(EntityPrototype? proto)
            => proto != null && protoMan.EnumerateAllParents<EntityPrototype>(proto.ID, includeSelf: true)
                .Any(p => p.id == "PartAnimal");

        var missing = new List<string>();
        await server.WaitAssertion(() =>
        {
            foreach (var prefab in protoMan.EnumeratePrototypes<BodyPrefabPrototype>())
            {
                var uid = entMan.Spawn(null, map.MapCoords);
                var body = new Entity<SLBodyComponent>(uid, entMan.AddComponent<SLBodyComponent>(uid));
                body.Comp.Prefab = prefab.ID;
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(bodySys.BuildBody(body), $"{prefab.ID} did not build");
                    Assert.That(bodySys.GetBodyParts(body), Has.Count.EqualTo(CountParts(prefab.Root)),
                        $"{prefab.ID}: part count does not match the prefab");
                }

                // A body whose root takes locational damage needs it on its torso, head and limbs too, bar animal
                // parts mixed in (slimes). Bodies without it at the root (animals, borgs, skeletons) are left alone.
                var locational = body.Comp.RootPartEntity is { } root && entMan.HasComponent<BodyPartDamageComponent>(root);
                foreach (var part in bodySys.GetBodyParts(body))
                {
                    if (!locational)
                        break;
                    if (IsAnimalPart(entMan.GetComponent<MetaDataComponent>(part).EntityPrototype))
                        continue;

                    if (part.Comp.Kind is BodyPartType.Torso or BodyPartType.Head or BodyPartType.Arm
                        or BodyPartType.Hand or BodyPartType.Leg or BodyPartType.Foot
                        && !entMan.HasComponent<BodyPartDamageComponent>(part))
                    {
                        missing.Add($"{prefab.ID}: {entMan.GetComponent<MetaDataComponent>(part).EntityPrototype?.ID}");
                    }
                }
                entMan.DeleteEntity(uid);
            }

            Assert.That(missing, Is.Empty, $"parts without {nameof(BodyPartDamageComponent)}:\n{string.Join("\n", missing)}");
        });
    }

    private static int CountParts(BodyPartDef def)
    {
        var count = 1;
        foreach (var children in new[] { def.AttachedParts, def.ContainedParts })
        {
            if (children == null)
                continue;
            foreach (var child in children.Values)
                count += CountParts(child);
        }

        return count;
    }
}
