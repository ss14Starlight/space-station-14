#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.NUnit.Constraints;
using Content.IntegrationTests.NUnit.Operators;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.GameTestTests;

public sealed class ConstraintsTests : GameTest
{
    #region Starlight
    [Test]
    [TestOf(typeof(CompExistsConstraint))]
    [TestOf(typeof(CompOperator))]
    [Description("Checks component presence, absence, and property constraints on one entity.")]
    [RunOnSide(Side.Server)]
    public void ComponentConstraintsCoverPresenceAndProperties()
    {
        var ent = SSpawn(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ent, Has.Comp<MetaDataComponent>(Server));
            Assert.That(ent,
                Has
                    .Comp<MetaDataComponent>(Server)
                    .Property(nameof(MetaDataComponent.EntityDeleted))
                    .EqualTo(false)
            );
            Assert.That(ent, Has.No.Comp<EyeComponent>(Server));
        }
    }
    #endregion

    #region Starlight
    [Test]
    [TestOf(typeof(LifeStageConstraint))]
    [Description("Checks deleted, missing, and live entity constraints in one scenario.")]
    [RunOnSide(Side.Server)]
    public void DeletedConstraintsCoverExistingAndMissingEntities()
    {
        var deleted = SSpawn(null);
        SDeleteNow(deleted);

        var neverExisted = new EntityUid(int.MaxValue / 2);
        var alive = SSpawn(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deleted, Is.Deleted(Server));
            Assert.That(neverExisted, Is.Deleted(Server), "Entities that never existed still count as deleted.");
            Assert.That(alive, Is.Not.Deleted(Server));
        }
    }
    #endregion
}
