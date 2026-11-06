#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.DragDrop;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._Starlight.Storage;

[TestFixture]
public sealed class CanSelfStoreTest : GameTest
{
    [TestPrototypes]
    private const string TestPrototypes = """
    - type: entity
      parent: ClothingBackpack
      id: CanSelfStoreTestStorage
      components:
      - type: Storage
        grid:
        - 0,0,40,30
        maxItemSize: Huge
    - type: entity
      parent: ClothingBackpack
      id: CanSelfStoreTestTooSmallStorage
      components:
      - type: Storage
        grid:
        - 0,0,5,4
        maxItemSize: Huge
    """;

    [Test]
    public async Task CharacterCanStoreItselfWhenStorageHasRoom()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid character = default;
        EntityUid storage = default;
        EntityUid tooSmallStorage = default;

        await server.WaitPost(() =>
        {
            character = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            storage = server.EntMan.SpawnEntity(
                "CanSelfStoreTestStorage",
                map.GridCoords.Offset(new Vector2(0.75f, 0f)));
            tooSmallStorage = server.EntMan.SpawnEntity(
                "CanSelfStoreTestTooSmallStorage",
                map.GridCoords.Offset(new Vector2(-0.75f, 0f)));
        });

        await RunTicksSync(1);

        await server.WaitPost(() =>
        {
            var fittingVerbs = GetAlternativeVerbs(server.EntMan, character, storage);
            var storeVerb = fittingVerbs.SingleOrDefault(verb => verb.Text == Loc.GetString("storage-verb-store-self"));
            Assert.That(storeVerb, Is.Not.Null);

            var tooSmallVerbs = GetAlternativeVerbs(server.EntMan, character, tooSmallStorage);
            Assert.That(tooSmallVerbs.Any(verb => verb.Text == Loc.GetString("storage-verb-store-self")), Is.False);

            storeVerb!.Act?.Invoke();
            storeVerb.Act?.Invoke();
            Assert.That(server.System<SharedContainerSystem>().IsEntityInContainer(character), Is.False);
        });

        await RunSeconds(4f);
        await server.WaitAssertion(() =>
            Assert.That(server.System<SharedContainerSystem>().IsEntityInContainer(character), Is.False));

        await RunSeconds(2f);

        await server.WaitAssertion(() =>
            Assert.That(server.System<SharedContainerSystem>().IsEntityInContainer(character), Is.True));
    }

    [Test]
    public async Task CharacterCanDragDropItselfIntoStorage()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid character = default;
        EntityUid storage = default;

        await server.WaitPost(() =>
        {
            character = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            storage = server.EntMan.SpawnEntity(
                "CanSelfStoreTestStorage",
                map.GridCoords.Offset(new Vector2(0.75f, 0f)));
        });

        await RunTicksSync(1);

        await server.WaitPost(() =>
        {
            var canDropArgs = new CanDropTargetEvent(character, character);
            server.EntMan.EventBus.RaiseLocalEvent(storage, ref canDropArgs);
            Assert.That(canDropArgs.CanDrop, Is.True);

            var dragDropArgs = new DragDropTargetEvent(character, character);
            server.EntMan.EventBus.RaiseLocalEvent(storage, ref dragDropArgs);
            Assert.That(dragDropArgs.Handled, Is.True);
            var repeatDragDropArgs = new DragDropTargetEvent(character, character);
            server.EntMan.EventBus.RaiseLocalEvent(storage, ref repeatDragDropArgs);
            Assert.That(repeatDragDropArgs.Handled, Is.True);
            Assert.That(server.System<SharedContainerSystem>().IsEntityInContainer(character), Is.False);
        });

        await RunSeconds(4f);
        await server.WaitAssertion(() =>
            Assert.That(server.System<SharedContainerSystem>().IsEntityInContainer(character), Is.False));

        await RunSeconds(2f);

        await server.WaitAssertion(() =>
            Assert.That(server.System<SharedContainerSystem>().IsEntityInContainer(character), Is.True));
    }

    private static AlternativeVerb[] GetAlternativeVerbs(IEntityManager entityManager, EntityUid user, EntityUid target)
    {
        var args = new GetVerbsEvent<AlternativeVerb>(
            user,
            target,
            @using: null,
            hands: null,
            canInteract: true,
            canComplexInteract: true,
            canAccess: true,
            extraCategories: []);
        entityManager.EventBus.RaiseLocalEvent(target, args);
        return args.Verbs.ToArray();
    }
}
