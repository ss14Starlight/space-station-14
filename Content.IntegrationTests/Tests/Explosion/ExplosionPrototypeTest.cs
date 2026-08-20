using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Utility;
using Content.Shared.Explosion;

namespace Content.IntegrationTests.Tests.Explosion;

public sealed class ExplosionPrototypeTest : GameTest
{
    [Test]
    public async Task ValidateExplosionPrototypes()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;

        var protos = protoMan.EnumeratePrototypes<ExplosionPrototype>();

        Assert.Multiple(() =>
        {
            foreach (var proto in protos)
            {
                Assert.That(proto._tileBreakChance, Is.Not.Empty, $"Empty tile break chance definitions for explosion prototype: {proto.ID}");
                Assert.That(proto._tileBreakChance, Has.Length.EqualTo(proto._tileBreakIntensity.Length), $"Malformed tile break chance definitions for explosion prototype: {proto.ID}");
            }
        });
    }
}
