using Content.IntegrationTests.Fixtures;
using Content.Server.Changeling.Systems;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Changeling;

public sealed class ChangelingDevourTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase("MobHuman")]
    [TestCase("MobMouse")]
    public async Task LivingAndDeadPreyCanBeDevoured(string prototype)
        => await Server.WaitAssertion(() =>
        {
            var ling = SpawnChangeling();
            var target = SEntMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
            var devour = Server.System<ChangelingDevourSystem>();
            var mobState = Server.System<MobStateSystem>();

            Assert.That(mobState.IsAlive(target), Is.True);
            Assert.That(devour.CanDevour(ling, target, showPopup: false), Is.True);

            mobState.ChangeMobState(target, MobState.Dead);
            Assert.That(devour.CanDevour(ling, target, showPopup: false), Is.True);

            SEntMan.AddComponent<RottingComponent>(target);
            Assert.That(devour.CanDevour(ling, target, showPopup: false), Is.False,
                "Allowing living prey must not allow rotten prey.");
            Assert.That(devour.CanDevour(ling, ling, showPopup: false), Is.False);

            var item = SEntMan.SpawnEntity("Pen", MapCoordinates.Nullspace);
            Assert.That(devour.CanDevour(ling, item, showPopup: false), Is.False);
        });

    [Test]
    public async Task StoredIdentityCannotBeDevouredAgain()
        => await Server.WaitAssertion(() =>
        {
            var ling = SpawnChangeling();
            var target = SEntMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var identity = SEntMan.GetComponent<ChangelingIdentityComponent>(ling);
            var devour = Server.System<ChangelingDevourSystem>();

            var clone = Server.System<ChangelingIdentitySystem>().CloneToPausedMap((ling, identity), target);
            Assert.That(clone, Is.Not.Null);
            Assert.That(devour.HasDevoured(ling, target), Is.True);
            Assert.That(devour.CanDevour(ling, target, showPopup: false), Is.False);
        });

    [Test]
    public async Task DevouredAnimalCannotBeDevouredAgainByTheSameChangeling()
        => await Server.WaitAssertion(() =>
        {
            var ling = SpawnChangeling();
            var otherLing = SpawnChangeling();
            var target = SEntMan.SpawnEntity("MobMouse", MapCoordinates.Nullspace);
            SEntMan.AddComponent<ChangelingDevouredComponent>(target).DevouredBy.Add(ling);
            var devour = Server.System<ChangelingDevourSystem>();

            Assert.That(devour.HasDevoured(ling, target), Is.True);
            Assert.That(devour.CanDevour(ling, target, showPopup: false), Is.False);
            Assert.That(devour.CanDevour(otherLing, target, showPopup: false), Is.True);
        });

    private EntityUid SpawnChangeling()
    {
        var ling = SEntMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
        SEntMan.AddComponent<ChangelingIdentityComponent>(ling);
        SEntMan.AddComponent<ChangelingDevourComponent>(ling);
        return ling;
    }
}
