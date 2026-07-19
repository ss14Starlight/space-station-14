using System.Linq;
using Content.Shared.Access;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sol.Medical.Virology;

[TestFixture]
public sealed class VirologyAccessAndJobTest
{
    [Test]
    public async Task VirologistJobRequirementsAndAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.That(proto.TryIndex<JobPrototype>("Virologist", out var job), Is.True);
            Assert.That(job!.Access.Any(a => a == "Virology"), Is.True);
            Assert.That(job.Access.Any(a => a == "Medical"), Is.True);
            Assert.That(job.RealDisplayWeight, Is.GreaterThan(
                proto.Index<JobPrototype>("Surgeon").RealDisplayWeight));

            Assert.That(proto.TryIndex<DepartmentPrototype>("Medical", out var dept), Is.True);
            var roles = dept!.Roles.Select(r => r.Id).ToList();
            var viro = roles.IndexOf("Virologist");
            var surg = roles.IndexOf("Surgeon");
            Assert.That(viro, Is.GreaterThanOrEqualTo(0));
            Assert.That(surg, Is.GreaterThanOrEqualTo(0));
            // Roadmap: Virologist immediately before Surgeon.
            Assert.That(viro, Is.LessThan(surg));
            Assert.That(surg, Is.EqualTo(viro + 1));

            Assert.That(proto.TryIndex<AccessLevelPrototype>("Virology", out _), Is.True);
            Assert.That(proto.TryIndex<AccessGroupPrototype>("AllAccess", out var all), Is.True);
            Assert.That(all!.Tags.Any(t => t == "Virology"), Is.True);

            Assert.That(proto.TryIndex<JobPrototype>("ChiefMedicalOfficer", out var cmo), Is.True);
            Assert.That(cmo!.Access.Any(a => a == "Virology"), Is.True);

            // Captain receives Virology via AllAccess.
            Assert.That(proto.TryIndex<JobPrototype>("Captain", out var captain), Is.True);
            Assert.That(captain!.Access.Any(a => a == "AllAccess") ||
                        captain.AccessGroups.Any(g => g == "AllAccess"), Is.True);

            // Ordinary doctors and chemists do not get dedicated Virology access.
            Assert.That(proto.TryIndex<JobPrototype>("MedicalDoctor", out var md), Is.True);
            Assert.That(md!.Access.Any(a => a == "Virology"), Is.False);
            Assert.That(md.ExtendedAccess.Any(a => a == "Virology"), Is.False);

            Assert.That(proto.TryIndex<JobPrototype>("Chemist", out var chemist), Is.True);
            Assert.That(chemist!.Access.Any(a => a == "Virology"), Is.False);
            Assert.That(chemist.ExtendedAccess.Any(a => a == "Virology"), Is.False);

            // Surgeon requirements stay independent of Virologist playtime.
            Assert.That(proto.TryGetMapping(typeof(JobPrototype), "Surgeon", out var surgeonMap), Is.True);
            Assert.That(surgeonMap!.ToString(), Does.Not.Contain("JobVirologist"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PathogenPrototypesResolve()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var id in new[] { "SolPathogenFlu", "SolPathogenWoundSepsis", "SolPathogenBioagent" })
            {
                Assert.That(proto.TryIndex<Content.Shared._Sol.Medical.Virology.PathogenPrototype>(id, out var pathogen), Is.True, id);
                Assert.That(pathogen!.Treatments, Is.Not.Empty);
                Assert.That(pathogen.Transmission, Is.Not.EqualTo(Content.Shared._Sol.Medical.Virology.PathogenTransmission.None));
            }
        });

        await pair.CleanReturnAsync();
    }
}
