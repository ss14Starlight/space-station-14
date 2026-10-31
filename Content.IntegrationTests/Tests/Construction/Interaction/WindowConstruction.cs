using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.IntegrationTests.Tests.Interaction;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Construction.Interaction;

public sealed class WindowConstruction : InteractionTest
{
    private const string Window = "Window";
    private const string RWindow = "ReinforcedWindow";

    #region Starlight
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";
    #endregion

    [Test]
    public async Task ConstructReinforcedWindow()
    {
        await StartConstruction(RWindow);
        await InteractUsing(RGlass, 5);
        ClientAssertPrototype(RWindow, Target);
    }

    #region Starlight
    [Test]
    public async Task DeconstructWindows()
    {
        // Starlight - start
        await StartDeconstruction(Window);
        await Interact(Screw, Wrench);
        AssertDeleted();
        await AssertEntityLookup((Glass, 2));

        await StartDeconstruction(RWindow);
        var damageSystem = SEntMan.System<DamageableSystem>();
        var damageType = Server.ProtoMan.Index(BluntDamageType);
        var damage = new DamageSpecifier(damageType, FixedPoint2.New(10));
        Assert.That(damageSystem.GetTotalDamage(STarget.Value), Is.EqualTo(FixedPoint2.Zero));
        await Server.WaitPost(() =>
            damageSystem.TryChangeDamage(SEntMan.GetEntity(Target).Value, damage, ignoreResistances: true));
        await RunTicks(5);
        Assert.That(damageSystem.GetTotalDamage(STarget.Value), Is.GreaterThan(FixedPoint2.Zero));
        await InteractUsing(Weld);
        Assert.That(damageSystem.GetTotalDamage(STarget.Value), Is.EqualTo(FixedPoint2.Zero));

        await Interact(
            Weld,
            Screw,
            Pry,
            Weld,
            Screw,
            Wrench);
        AssertDeleted();
        await AssertEntityLookup((Glass, 2), (RGlass, 2));
        // Starlight - end
    }
    #endregion
}
