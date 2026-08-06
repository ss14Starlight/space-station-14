using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Virology;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Starlight.Medical.Virology;

/// <summary>
/// Every person-to-person bacterial route is a one-line patch into an upstream file -
/// melee, pulling, cuffing, healing and friendly interaction all live in core systems
/// rather than under _Starlight. An upstream merge can drop one silently and nothing
/// else would fail, and a reviewer already reported being unable to find three of them.
/// These tests turn that into a build failure.
/// </summary>
[TestFixture]
public sealed class PathogenContactHookTests : GameTest
{
    [Test]
    public async Task MeleeHitTransmitsBacteria()
    {
        await AssertContactTransmits((entities, carrier, target) =>
        {
            // Subscribed on the weapon, and an unarmed attacker is their own weapon.
            var hit = new MeleeHitEvent(
                new List<EntityUid> { target },
                carrier,
                carrier,
                new DamageSpecifier(),
                null);

            entities.EventBus.RaiseLocalEvent(carrier, hit);
        });
    }

    [Test]
    public async Task PullingTransmitsBacteria()
    {
        await AssertContactTransmits((entities, carrier, target) =>
        {
            var pulling = entities.System<PullingSystem>();
            Assert.That(
                pulling.TryStartPull(carrier, target),
                Is.True,
                "The pull has to actually start for the hook to mean anything.");
        });
    }

    [Test]
    public async Task FriendlyInteractionTransmitsBacteria()
    {
        await AssertContactTransmits((entities, carrier, target) =>
        {
            // The hook sits behind a success roll, but humanoids hug at successChance: 1,
            // so this is deterministic without touching the component.
            Assert.That(
                entities.GetComponent<InteractionPopupComponent>(target).SuccessChance,
                Is.EqualTo(1f),
                "Humanoids are meant to always succeed at hugging; a lower chance makes this test flaky.");

            entities.EventBus.RaiseLocalEvent(target, new InteractHandEvent(carrier, target));
        });
    }

    /// <summary>
    /// Spawns an infected carrier next to a clean target, runs <paramref name="interact"/>,
    /// and asserts the strain crossed. Transmissibility is pinned to certainty so a miss
    /// means the hook is gone rather than that the dice were unkind.
    /// </summary>
    private async Task AssertContactTransmits(Action<IEntityManager, EntityUid, EntityUid> interact)
    {
        var server = Pair.Server;
        var entities = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var pathogens = server.System<PathogenSystem>();
        var registry = server.System<PathogenRegistrySystem>();

        EntityUid carrier = default;
        EntityUid target = default;
        Pathogen strain = default!;

        await server.WaitPost(() =>
        {
            maps.CreateMap(out var mapId);
            carrier = entities.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, mapId));
            target = entities.SpawnEntity("MobHuman", new MapCoordinates(Vector2.UnitX, mapId));

            // Prevalence is a fraction of living crew, so an empty map would block the
            // infection for a reason that has nothing to do with the hook.
            for (var i = 0; i < 12; i++)
            {
                entities.SpawnEntity("MobHuman", new MapCoordinates(new Vector2(100 + i, 0), mapId));
            }

            strain = registry.Generate("ThroatRot")!;
            strain.Transmissibility = 1f;
            strain.MaxPrevalence = 1f;

            Assert.That(pathogens.TryInfect(carrier, strain.Id), Is.True);
            Assert.That(
                pathogens.IsInfected(target, strain.Id),
                Is.False,
                "The target must start clean or the assertion below proves nothing.");
        });

        // Friendly interaction is rate limited against the game clock, which starts near
        // zero in a test, so the hug is swallowed unless time has actually passed.
        await server.WaitRunTicks(60);

        await server.WaitAssertion(() =>
        {
            interact(entities, carrier, target);

            Assert.That(
                pathogens.IsInfected(target, strain.Id),
                Is.True,
                "Physical contact with a bacterial carrier must transmit.");
        });
    }
}
