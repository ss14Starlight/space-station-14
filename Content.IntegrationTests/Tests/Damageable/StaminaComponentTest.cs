using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Utility;
using Content.Shared.Damage.Components;

namespace Content.IntegrationTests.Tests.Damageable;

public sealed class StaminaComponentTest : GameTest
{
    [Test]
    public async Task ValidatePrototypes()
    {
        var pair = Pair;
        var server = pair.Server;

        var protos = pair.GetPrototypesWithComponent<StaminaComponent>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var (proto, comp) in protos)
                {
                    Assert.That(comp.AnimationThreshold, Is.LessThan(comp.CritThreshold),
                        $"Animation threshold on {proto.ID} must be less than its crit threshold.");
                }
            });
        });
    }
}
