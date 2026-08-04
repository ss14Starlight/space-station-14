using Content.IntegrationTests.Fixtures;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Containers;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Access;

public sealed class VirologyAccessTest : GameTest
{
    private static readonly ProtoId<AccessLevelPrototype> Virology = "Virology";
    private static readonly ProtoId<JobPrototype> VirologistJob = "Virologist";
    private static readonly ProtoId<JobPrototype> ChiefMedicalOfficerJob = "ChiefMedicalOfficer";
    private static readonly ProtoId<AccessGroupPrototype> MedicalGroup = "Medical";
    private static readonly ProtoId<AccessGroupPrototype> AllAccessGroup = "AllAccess";
    private static readonly ProtoId<AccessGroupPrototype> CyborgAllAccessGroup = "CyborgAllAccess";

    [Test]
    public async Task VirologyAccessIsWiredToJobsAndSecureEntities()
    {
        var server = Pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var components = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            Assert.That(prototypes.HasIndex(Virology), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(prototypes.Index(VirologistJob).Access, Does.Contain(Virology));
                Assert.That(prototypes.Index(ChiefMedicalOfficerJob).Access, Does.Contain(Virology));
                Assert.That(prototypes.Index(MedicalGroup).Tags, Does.Contain(Virology));
                Assert.That(prototypes.Index(AllAccessGroup).Tags, Does.Contain(Virology));
                Assert.That(prototypes.Index(CyborgAllAccessGroup).Tags, Does.Contain(Virology));
            });

            AssertReader(prototypes, components, "DoorElectronicsVirology");
            AssertReader(prototypes, components, "LockerVirologist");
            AssertDoorBoard(prototypes, components, "AirlockVirologyLocked");
            AssertDoorBoard(prototypes, components, "AirlockVirologyGlassLocked");
        });
    }

    private static void AssertReader(
        IPrototypeManager prototypes,
        IComponentFactory components,
        EntProtoId prototypeId)
    {
        var prototype = prototypes.Index<EntityPrototype>(prototypeId);
        Assert.That(
            prototype.TryGetComponent<AccessReaderComponent>(out var reader, components),
            Is.True,
            $"{prototypeId} should have an access reader");
        Assert.That(reader!.AccessLists, Has.Count.EqualTo(1));
        Assert.That(reader.AccessLists[0], Is.EquivalentTo(new[] { Virology }));
    }

    private static void AssertDoorBoard(
        IPrototypeManager prototypes,
        IComponentFactory components,
        EntProtoId prototypeId)
    {
        var prototype = prototypes.Index<EntityPrototype>(prototypeId);
        Assert.That(
            prototype.TryGetComponent<ContainerFillComponent>(out var fill, components),
            Is.True,
            $"{prototypeId} should fill its electronics container");
        Assert.That(fill!.Containers["board"], Is.EqualTo(new[] { "DoorElectronicsVirology" }));
    }
}
