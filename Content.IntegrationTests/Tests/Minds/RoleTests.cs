using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Roles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Reflection;

namespace Content.IntegrationTests.Tests.Minds;

[TestFixture]
public sealed class RoleTests : GameTest
{
    #region Starlight
    /// <summary>
    /// Checks the configuration of all role prototypes and their related role components.
    /// </summary>
    [Test]
    public async Task ValidateRolePrototypeConfiguration()
    {
        var pair = Pair;

        var componentFactory = pair.Server.ResolveDependency<IComponentFactory>();
        var jobComp = componentFactory.GetComponentName<JobRoleComponent>();
        var mindCompId = componentFactory.GetComponentName<MindRoleComponent>();
        var refMan = pair.Server.ResolveDependency<IReflectionManager>();
        var compTypes = refMan.GetAllChildren(typeof(BaseMindRoleComponent))
            .Append(typeof(RoleBriefingComponent))
            .Where(x => !x.IsAbstract);

        Assert.Multiple(() =>
        {
            foreach (var (proto, comp) in pair.GetPrototypesWithComponent<MindRoleComponent>())
            {
                Assert.That(comp.AntagPrototype == null || comp.JobPrototype == null, $"Role {proto.ID} has both a job and antag prototype.");
                Assert.That(!comp.ExclusiveAntag || comp.Antag, $"Role {proto.ID} is marked as an exclusive antag, despite not being an antag.");
                Assert.That(comp.Antag || comp.AntagPrototype == null, $"Role {proto.ID} has an antag prototype, despite not being an antag.");

                if (comp.JobPrototype != null)
                    Assert.That(proto.Components.ContainsKey(jobComp), $"Role {proto.ID} is a job, despite not having a job prototype.");

                // It is possible that this is meant to be supported? Though I would assume that it would be for
                // admin / prototype uploads, and that pre-defined roles should still check this.
                Assert.That(!comp.Antag || comp.AntagPrototype != null, $"Role {proto.ID} is an antag, despite not having an antag prototype.");
            }

            foreach (var (proto, _) in pair.GetPrototypesWithComponent<JobRoleComponent>())
            {
                if (proto.Components.TryGetComponent(mindCompId, out var mindComp))
                    Assert.That(((MindRoleComponent)mindComp).JobPrototype, Is.Not.Null);
            }

            foreach (var comp in compTypes)
            {
                foreach (var proto in pair.GetPrototypesWithComponent(comp))
                {
                    Assert.That(proto.Components.ContainsKey(mindCompId), $"Role {proto.ID} does not have a {nameof(MindRoleComponent)} despite having a {comp.Name}");
                }
            }
        });
    }
    #endregion
}
