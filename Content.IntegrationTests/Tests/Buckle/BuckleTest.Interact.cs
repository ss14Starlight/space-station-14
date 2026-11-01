using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Buckle;

public sealed partial class BuckleTest
{
    #region Starlight
    [Test]
    public async Task BuckleInteractTogglesSelfAndUnbucklesOthers()
    {
        var pair = Pair;
        var server = pair.Server;

        var entMan = server.ResolveDependency<IServerEntityManager>();
        var buckleSystem = entMan.System<SharedBuckleSystem>();

        EntityUid user = default;
        EntityUid victim = default;
        EntityUid chair = default;
        BuckleComponent userBuckle = null;
        BuckleComponent victimBuckle = null;
        StrapComponent strap = null;

        await server.WaitAssertion(() =>
        {
            user = entMan.SpawnEntity(BuckleDummyId, MapCoordinates.Nullspace);
            victim = entMan.SpawnEntity(BuckleDummyId, MapCoordinates.Nullspace);
            chair = entMan.SpawnEntity(StrapDummyId, MapCoordinates.Nullspace);

            Assert.That(entMan.TryGetComponent(user, out userBuckle));
            Assert.That(entMan.TryGetComponent(victim, out victimBuckle));
            Assert.That(entMan.TryGetComponent(chair, out strap));
        });

        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(chair, new InteractHandEvent(user, chair));
            Assert.Multiple(() =>
            {
                Assert.That(userBuckle.BuckledTo, Is.EqualTo(chair));
                Assert.That(userBuckle.Buckled);
                Assert.That(strap.BuckledEntities, Does.Contain(user));
            });
        });

        await server.WaitRunTicks(20);

        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(chair, new InteractHandEvent(user, chair));
            Assert.Multiple(() =>
            {
                Assert.That(userBuckle.BuckledTo, Is.Null);
                Assert.That(userBuckle.Buckled, Is.False);
                Assert.That(strap.BuckledEntities, Does.Not.Contain(user));
            });

            // Buckle the victim, then interact with the chair to unbuckle them.
            Assert.That(buckleSystem.TryBuckle(victim, user, chair, victimBuckle));
            Assert.Multiple(() =>
            {
                Assert.That(victimBuckle.BuckledTo, Is.EqualTo(chair));
                Assert.That(victimBuckle.Buckled);
                Assert.That(strap.BuckledEntities, Does.Contain(victim));
            });
        });

        await server.WaitRunTicks(20);

        await server.WaitAssertion(() =>
        {
            entMan.EventBus.RaiseLocalEvent(chair, new InteractHandEvent(user, chair));
            Assert.Multiple(() =>
            {
                Assert.That(victimBuckle.BuckledTo, Is.Null);
                Assert.That(victimBuckle.Buckled, Is.False);
                Assert.That(strap.BuckledEntities, Does.Not.Contain(victim));
            });
        });
    }
    #endregion
}
