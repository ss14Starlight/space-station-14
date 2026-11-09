#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server.GameTicking.Rules;
using Content.Server.Implants;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared.Antag;
using Content.Shared.Flash;
using Content.Shared.Mindshield.Components;
using Content.Shared.Preferences;
using Content.Shared.Revolutionary.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.GameRules;

[TestFixture]
[TestOf(typeof(RevolutionaryRuleSystem))]
public sealed class RevolutionaryConversionTest : GameTest
{
    // Conversions spawn uplinks, minds and a delayed timer that the fixture doesn't track.
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private static readonly ProtoId<AntagSpecifierPrototype> _headRevSpecifier = "HeadRev";
    private static readonly EntProtoId _mindShieldImplant = "MindShieldImplant";

    [Test]
    public async Task FlashConversionRespectsProtections()
    {
        var testMap = await Pair.CreateTestMap();

        var mindSys = Server.System<MindSystem>();
        var implantSys = Server.System<SubdermalImplantSystem>();
        var spawnSys = Server.System<StationSpawningSystem>();

        EntityUid headRev = default;
        EntityUid human = default;
        EntityUid shieldedHuman = default;
        EntityUid borgi = default;
        EntityUid borg = default;
        EntityUid k9 = default;
        EntityUid shieldedCorgi = default;

        await Server.WaitAssertion(() =>
        {
            var coords = testMap.GridCoords;

            headRev = SEntMan.SpawnAtPosition("MobHuman", coords);
            SEntMan.AddComponents(headRev, SProtoMan.Index(_headRevSpecifier).Components);
            Assert.That(SEntMan.GetComponent<HeadRevolutionaryComponent>(headRev).Blacklist, Is.Not.Null);

            human = SpawnWithMind("MobHuman", coords);

            shieldedHuman = SpawnWithMind("MobHuman", coords);
            implantSys.AddImplant(shieldedHuman, _mindShieldImplant);

            borgi = SpawnWithMind("StationBorgiChassis", coords);
            borg = SpawnWithMind("PlayerBorgChassis", coords);

            k9 = spawnSys.SpawnPlayerMob(coords, "K9", new HumanoidCharacterProfile(), station: null);
            GiveMind(k9);

            shieldedCorgi = SpawnWithMind("MobCorgiSmart", coords);
            implantSys.AddImplant(shieldedCorgi, _mindShieldImplant);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<MindShieldComponent>(shieldedHuman));
                Assert.That(SEntMan.HasComponent<MindShieldComponent>(k9), "K9 should spawn with a mindshield.");
                Assert.That(SEntMan.HasComponent<MindShieldComponent>(shieldedCorgi));
            });

            foreach (var target in new[] { human, shieldedHuman, borgi, borg, k9, shieldedCorgi })
            {
                var ev = new AfterFlashedEvent(target, headRev, null, true);
                SEntMan.EventBus.RaiseLocalEvent(headRev, ref ev);
            }

            Assert.Multiple(() =>
            {
                AssertRev(human, true);
                AssertRev(shieldedHuman, false);
                AssertRev(borgi, true);
                AssertRev(borg, false);
                AssertRev(k9, false);
                AssertRev(shieldedCorgi, false);
            });
        });

        // Let the delayed conversion popup fire inside the test.
        await RunSeconds(2);

        EntityUid SpawnWithMind(string proto, EntityCoordinates coords)
        {
            var uid = SEntMan.SpawnAtPosition(proto, coords);
            GiveMind(uid);
            return uid;
        }

        void GiveMind(EntityUid uid)
        {
            var mind = mindSys.CreateMind(null);
            mindSys.TransferTo(mind, uid, mind: mind);
        }

        void AssertRev(EntityUid uid, bool expected) => Assert.That(SEntMan.HasComponent<RevolutionaryComponent>(uid), Is.EqualTo(expected),
                $"{SToPrettyString(uid)} {(expected ? "should" : "should not")} be converted.");
    }
}
