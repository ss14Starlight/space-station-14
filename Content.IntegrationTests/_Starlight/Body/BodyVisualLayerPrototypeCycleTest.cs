// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

#nullable enable
using Content.Client._Starlight.Sprite;
using Content.IntegrationTests.Fixtures;

namespace Content.IntegrationTests._Starlight.Body;

[TestFixture]
public sealed class BodyVisualLayerPrototypeCycleTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true };

    [Test]
    public async Task NoCircularDependencies()
    {
        var system = Pair.Client.System<VisualLayerSystem>();

        Assert.That(system.CyclicLayers, Is.Empty,
            $"Circular dependency detected in VisualLayerPrototype(s): {string.Join(", ", system.CyclicLayers)}");
    }
}
