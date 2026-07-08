using System.Collections.Generic;
using Content.Shared._Starlight.Spawners.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.Tests._Starlight.Spawner;

[TestOf(typeof(TimedDespawnComponent))]
[TestOf(typeof(TimedSpawnerComponent))]
public sealed partial class TimedSpawnerDespawnTest
{
    const double Tolerance = 1.0;

    [Test]
    public async Task TestDespawnWhenDone()
    {
        await using var pair = await PoolManager.GetServerClient();

        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();
        var compFactory = pair.Server.ResolveDependency<IComponentFactory>();
        var prototypes = protoMan.EnumeratePrototypes<EntityPrototype>();

        var errors = new List<string>();

        foreach (var proto in prototypes)
        {
            if (!proto.Components.TryGetComponent<TimedSpawnerComponent>(compFactory, out var spawner) || !proto.Components.TryGetComponent<TimedDespawnComponent>(compFactory, out var despawner))
                continue;

            if (Math.Abs(despawner.Lifetime - spawner.IntervalSeconds.TotalSeconds) <= Tolerance)
                errors.Add(
                    $"Prototype {proto.ID} has a TimedSpawnerComponent with DespawnWhenDone = {spawner.DespawnWhenDone}, " +
                    $"but Lifetime ≈ IntervalSeconds (±{Tolerance}s). This is unnecessary — use DespawnWhenDone instead.");
        }

        Assert.That(errors, Is.Empty, string.Join("\n", errors));

        await pair.CleanReturnAsync();
    }
}
