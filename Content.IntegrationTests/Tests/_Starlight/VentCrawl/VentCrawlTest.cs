using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.VentCrawl.Components;
using Content.Shared._Starlight.VentCrawl.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.VentCrawl;

[TestOf(typeof(SharedVentCrawlSystem))]
public sealed class VentCrawlTest : GameTest
{
    private const string VentProto = "GasVentPump";
    private const string CrawlerProto = "MobCockroach";

    private EntityUid _vent;
    private EntityUid _crawler;

    private async Task SpawnAndEnter()
    {
        var map = await Pair.CreateTestMap();
        var xformSystem = Server.System<SharedTransformSystem>();
        var vents = Server.System<SharedVentCrawlSystem>();

        await Server.WaitAssertion(() =>
        {
            _vent = SEntMan.SpawnEntity(VentProto, map.GridCoords);
            var xform = SEntMan.GetComponent<TransformComponent>(_vent);
            if (!xform.Anchored)
                xformSystem.AnchorEntity((_vent, xform));

            _crawler = SEntMan.SpawnEntity(CrawlerProto, map.GridCoords);

            Assert.That(vents.TryInsert(_vent, _crawler), "The crawler could not enter an anchored vent.");
            Assert.That(SEntMan.GetComponent<VentCrawlerComponent>(_crawler).InTube);
            Assert.That(Server.System<SharedContainerSystem>().IsEntityInContainer(_crawler));
        });

        await RunTicksSync(5);
    }

    private int Holders()
    {
        var count = 0;
        var query = SEntMan.EntityQueryEnumerator<VentCrawlHolderComponent>();
        while (query.MoveNext(out _, out _))
            count++;

        return count;
    }

    private void AssertCrawlerIsOut()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.EntityExists(_crawler), "The crawler was deleted with the vent.");
            Assert.That(Server.System<SharedContainerSystem>().IsEntityInContainer(_crawler), Is.False, "The crawler is still inside a holder.");
            Assert.That(SEntMan.GetComponent<VentCrawlerComponent>(_crawler).InTube, Is.False);
            Assert.That(SEntMan.HasComponent<BeingVentCrawlComponent>(_crawler), Is.False);
            Assert.That(SEntMan.GetComponent<TransformComponent>(_crawler).GridUid, Is.Not.Null, "The crawler did not land back on the grid.");
            Assert.That(Holders(), Is.Zero, "A vent crawl holder was left behind.");
        });
    }

    [Test]
    public async Task ExitReturnsCrawlerToGrid()
    {
        await SpawnAndEnter();

        await Server.WaitPost(() =>
        {
            var holder = SEntMan.GetComponent<BeingVentCrawlComponent>(_crawler).Holder;
            Server.System<SharedVentCrawlSystem>().ExitVentCrawl(holder);
        });

        await RunTicksSync(5);
        await Server.WaitAssertion(AssertCrawlerIsOut);
    }

    [Test]
    public async Task DeletingVentEjectsCrawler()
    {
        await SpawnAndEnter();

        await Server.WaitPost(() => SEntMan.DeleteEntity(_vent));

        await RunTicksSync(5);
        await Server.WaitAssertion(AssertCrawlerIsOut);
    }

    [Test]
    public async Task UnanchoringVentEjectsCrawler()
    {
        await SpawnAndEnter();

        await Server.WaitPost(() => Server.System<SharedTransformSystem>().Unanchor(_vent));

        await RunTicksSync(5);
        await Server.WaitAssertion(AssertCrawlerIsOut);
    }

    [Test]
    public async Task DeletingCrawlerRemovesHolder()
    {
        await SpawnAndEnter();

        await Server.WaitPost(() => SEntMan.DeleteEntity(_crawler));

        await RunTicksSync(5);
        await Server.WaitAssertion(() => Assert.That(Holders(), Is.Zero, "A vent crawl holder outlived its crawler."));
    }
}
