// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

#nullable enable
using Content.Client._Starlight.Body.Systems;
using Content.Client._Starlight.Sprite;

namespace Content.IntegrationTests._Starlight.Body;

[TestFixture]
public sealed class BodyVisualLayerPrototypeCycleTest
{
    [Test]
    public async Task NoCircularDependencies()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        var system = client.System<VisualLayerSystem>();

        Assert.That(system.CyclicLayers, Is.Empty,
            $"Circular dependency detected in BodyVisualLayerPrototype(s): {string.Join(", ", system.CyclicLayers)}");

        await pair.CleanReturnAsync();
    }
}
