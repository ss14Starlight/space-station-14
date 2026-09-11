using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Blimpuf.Contraband;
using Content.Shared._Starlight.ScanGate.Components;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Blimpuf;

[TestFixture]
public sealed class ContrabandTest : GameTest
{
    private const string InheritanceChild = "ContrabandInheritanceChild";
    private const string ClearanceTarget = "ContrabandClearanceTarget";
    private const string ReagentInheritanceChild = "ContrabandReagentInheritanceChild";
    private const string PrescriptionReagent = "ContrabandPrescriptionReagent";
    private static readonly ProtoId<JobPrototype> ChemistJob = "Chemist";
    private static readonly ProtoId<JobPrototype> DetectiveJob = "Detective";

    [TestPrototypes]
    private const string TestPrototypes = """
        - type: entity
          id: ContrabandAuthorizationParent
          parent: BaseRestrictedContraband
          abstract: true
          components:
          - type: Contraband
            contrabandType: Syndicate
            allowedDepartments: [ Security ]
            allowedJobs: [ Detective ]

        - type: entity
          id: ContrabandAdditionalAuthorizationParent
          parent: BaseRestrictedContraband
          abstract: true
          components:
          - type: Contraband
            allowedDepartments: [ Command ]
            allowedJobs: [ Warden ]

        - type: entity
          id: ContrabandTierParent
          abstract: true
          components:
          - type: ScanDetectable
          - type: Contraband
            tier: Tier3

        - type: entity
          id: ContrabandInheritanceChild
          parent: [ ContrabandAuthorizationParent, ContrabandAdditionalAuthorizationParent, ContrabandTierParent ]
          components:
          - type: Contraband
            contrabandType: Magical
            allowedDepartments: [ Medical ]
            allowedJobs: [ Chemist ]

        - type: reagent
          id: ContrabandReagentInheritanceParent
          abstract: true
          name: reagent-name-water
          desc: reagent-desc-water
          physicalDesc: reagent-physical-desc-translucent
          contrabandTier: Tier1
          contrabandType: Syndicate
          allowedDepartments: [ Security ]
          allowedJobs: [ Detective ]

        - type: reagent
          id: ContrabandReagentInheritanceChild
          parent: ContrabandReagentInheritanceParent
          contrabandTier: Tier2
          contrabandType: Magical
          allowedDepartments: [ Medical ]
          allowedJobs: [ Chemist ]

        - type: reagent
          id: ContrabandPrescriptionReagent
          name: reagent-name-water
          desc: reagent-desc-water
          physicalDesc: reagent-physical-desc-translucent
          contrabandTier: Tier1
          requiresPrescription: true

        - type: entity
          id: ContrabandClearanceTarget
          components:
          - type: Contraband
            allowedDepartments: [ Security ]
            allowedJobs: [ Detective ]
        """;

    [Test]
    public async Task ContrabandPrototypesAreValid()
    {
        var pair = Pair;
        var client = pair.Client;
        var protoMan = client.ResolveDependency<IPrototypeManager>();
        var componentFactory = client.ResolveDependency<IComponentFactory>();

        await client.WaitAssertion(() =>
        {
            foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || pair.IsTestPrototype(proto) ||
                    !proto.TryGetComponent<ContrabandComponent>(out var contraband, componentFactory))
                {
                    continue;
                }

                Assert.That(protoMan.HasIndex(contraband.Tier), Is.True,
                    $"{proto.ID} has an unknown contraband tier {contraband.Tier}.");

                if (contraband.ContrabandType is { } type)
                {
                    Assert.That(protoMan.HasIndex(type), Is.True,
                        $"{proto.ID} has an unknown contraband type {type}.");
                }
            }

            foreach (var reagent in protoMan.EnumeratePrototypes<ReagentPrototype>())
            {
                if (pair.IsTestPrototype(reagent))
                    continue;

                var hasAuthorization = reagent.AllowedDepartments.Count > 0 ||
                                       reagent.AllowedJobs.Count > 0 ||
                                       reagent.RequiresPrescription;
                if (reagent.ContrabandTier is not { } tier)
                {
                    Assert.That(hasAuthorization || reagent.ContrabandType is not null, Is.False,
                        $"{reagent.ID} has contraband authorization/type data but no contraband tier.");
                    continue;
                }

                Assert.That(protoMan.HasIndex(tier), Is.True,
                    $"{reagent.ID} has an unknown contraband tier {tier}.");

                if (reagent.ContrabandType is { } type)
                {
                    Assert.That(protoMan.HasIndex(type), Is.True,
                        $"{reagent.ID} has an unknown contraband type {type}.");
                }
            }
        });
    }

    [Test]
    public async Task ContrabandInheritanceCombinesClassificationAndAuthorization()
    {
        var client = Pair.Client;
        var protoMan = client.ResolveDependency<IPrototypeManager>();
        var componentFactory = client.ResolveDependency<IComponentFactory>();

        await client.WaitAssertion(() =>
        {
            var entity = protoMan.Index<EntityPrototype>(InheritanceChild);
            Assert.That(entity.TryGetComponent<ContrabandComponent>(out var contraband, componentFactory), Is.True);
            Assert.That(entity.TryGetComponent<ScanDetectableComponent>(out _, componentFactory), Is.True);

            var reagent = protoMan.Index<ReagentPrototype>(ReagentInheritanceChild);

            Assert.That(contraband.Tier.Id, Is.EqualTo("Tier3"));
            Assert.That(contraband.ContrabandType?.Id, Is.EqualTo("Magical"));
            Assert.That(contraband.AllowedDepartments,
                Is.EquivalentTo(new ProtoId<DepartmentPrototype>[] { "Security", "Command", "Medical" }));
            Assert.That(contraband.AllowedJobs,
                Is.EquivalentTo(new ProtoId<JobPrototype>[] { "Detective", "Warden", "Chemist" }));

            Assert.That(reagent.ContrabandTier?.Id, Is.EqualTo("Tier2"));
            Assert.That(reagent.ContrabandType?.Id, Is.EqualTo("Magical"));
            Assert.That(reagent.AllowedDepartments,
                Is.EquivalentTo(new ProtoId<DepartmentPrototype>[] { "Security", "Medical" }));
            Assert.That(reagent.AllowedJobs,
                Is.EquivalentTo(new ProtoId<JobPrototype>[] { "Detective", "Chemist" }));
        });
    }

    // Cover parent-order-sensitive equipment and the department-specific versions of shared items.
    [TestCase("EncryptionKeyStationMaster", "Tier4", "CentralCommand", true, new[] { "Command", "CentralCommand" }, new string[0])]
    [TestCase("WeaponEnergyShotgun", "Tier3", null, true, new[] { "Command" }, new[] { "Warden" })]
    [TestCase("ClothingHandsMercGlovesCombat", "Tier2", null, false, new[] { "Engineering", "Security", "Command" }, new[] { "SalvageSpecialist", "SalvageLead", "MiningSpecialist" })]
    [TestCase("EncryptionKeySecurity", "Tier1", null, false, new[] { "Security" }, new[] { "IAA" })]
    [TestCase("GreenLightShield", "Tier4", "CentralCommand", true, new[] { "CentralCommand" }, new string[0])]
    [TestCase("BlueLightShield", "Tier3", "CentralCommand", false, new[] { "Representatives" }, new string[0])]
    [TestCase("BoxFolderCentComClipboard", "Tier1", null, false, new[] { "Representatives", "CentralCommand" }, new string[0])]
    public async Task EquipmentHasExpectedContrabandPermissions(
        string prototype, string tier, string type, bool detectable, string[] departments, string[] jobs)
    {
        var client = Pair.Client;
        var protoMan = client.ResolveDependency<IPrototypeManager>();
        var componentFactory = client.ResolveDependency<IComponentFactory>();

        await client.WaitAssertion(() =>
        {
            var entity = protoMan.Index<EntityPrototype>(prototype);
            Assert.That(entity.TryGetComponent<ContrabandComponent>(out var contraband, componentFactory), Is.True);
            Assert.That(contraband.Tier.Id, Is.EqualTo(tier));
            Assert.That(contraband.ContrabandType?.Id, Is.EqualTo(type));
            Assert.That(contraband.AllowedDepartments.Select(id => id.Id), Is.EquivalentTo(departments));
            Assert.That(contraband.AllowedJobs.Select(id => id.Id), Is.EquivalentTo(jobs));
            Assert.That(entity.TryGetComponent<ScanDetectableComponent>(out _, componentFactory), Is.EqualTo(detectable));
        });
    }

    [Test]
    public async Task ContrabandMessagesDescribeClassificationAndAuthorization()
    {
        var client = Pair.Client;
        var systemManager = client.ResolveDependency<IEntitySystemManager>();

        await client.WaitAssertion(() =>
        {
            var contraband = systemManager.GetEntitySystem<ContrabandSystem>();

            var itemClassification = contraband.GetClassificationMessage("Tier1", null, ContrabandItemType.Item);
            var reagentClassification = contraband.GetClassificationMessage(
                "Tier2",
                "Magical",
                ContrabandItemType.Reagent);
            var itemAuthorization = contraband.GetAuthorizationMessage(
                ["Security"],
                ["Detective"],
                ContrabandItemType.Item);
            var prescriptionAuthorization = contraband.GetAuthorizationMessage(
                [],
                [],
                ContrabandItemType.Reagent,
                true);

            Assert.That(itemClassification, Does.Contain("restricted tier 1 contraband"));
            Assert.That(itemClassification, Does.Contain("piece"));
            Assert.That(itemClassification, Does.Not.Contain("reagent"));

            Assert.That(reagentClassification, Does.Contain("heavily regulated tier 2 magical contraband reagent"));

            Assert.That(itemAuthorization, Does.Contain("This item is restricted"));
            Assert.That(itemAuthorization, Does.Contain("Security"));
            Assert.That(itemAuthorization, Does.Contain("Detective"));

            Assert.That(prescriptionAuthorization, Does.Contain("This reagent is restricted"));
            Assert.That(prescriptionAuthorization, Does.Contain("patients with a valid prescription"));
        });
    }

    [TestCase("Water", true, false)]
    [TestCase(PrescriptionReagent, true, true)]
    [TestCase(PrescriptionReagent, false, false)]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.ContrabandExamineOnlyInHUD), true)]
    public async Task ReagentDescriptionsRespectClassificationAndDisplaySetting(
        string prototype, bool enabled, bool showsDescription)
    {
        await OverrideCVar(Side.Server, CCVars.ContrabandExamine, enabled);
        var client = Pair.Client;
        var protoMan = client.ResolveDependency<IPrototypeManager>();
        var systemManager = client.ResolveDependency<IEntitySystemManager>();

        await client.WaitAssertion(() =>
        {
            var contraband = systemManager.GetEntitySystem<ContrabandSystem>();
            var message = new FormattedMessage();
            message.AddText("Reagent contents");
            contraband.AppendReagentDescription(message, protoMan.Index<ReagentPrototype>(prototype));
            var expected = "Reagent contents";
            if (showsDescription)
            {
                expected += "\nThis is a restricted tier 1 contraband reagent." +
                            "\nThis reagent is restricted to patients with a valid prescription.";
            }

            Assert.That(message.ToString(), Is.EqualTo(expected));
        });
    }

    [Test]
    public async Task ContrabandExamineReflectsIdCardClearance()
    {
        var client = Pair.Client;
        var entMan = client.ResolveDependency<IEntityManager>();
        var protoMan = client.ResolveDependency<IPrototypeManager>();

        await client.WaitAssertion(() =>
        {
            var target = CSpawn(ClearanceTarget);

            var noIdUser = CSpawn(null);

            var departmentUser = CSpawn(null);
            var departmentId = entMan.EnsureComponent<IdCardComponent>(departmentUser);
            departmentId.JobDepartments = ["Security"];
            departmentId.LocalizedJobTitle = protoMan.Index(ChemistJob).LocalizedName;

            var jobUser = CSpawn(null);
            var jobId = entMan.EnsureComponent<IdCardComponent>(jobUser);
            jobId.JobDepartments = ["Medical"];
            jobId.LocalizedJobTitle = protoMan.Index(DetectiveJob).LocalizedName;

            var mismatchedUser = CSpawn(null);
            var mismatchedId = entMan.EnsureComponent<IdCardComponent>(mismatchedUser);
            mismatchedId.JobDepartments = ["Medical"];
            mismatchedId.LocalizedJobTitle = protoMan.Index(ChemistJob).LocalizedName;

            AssertClearance(GetContrabandVerb(entMan, noIdUser, target), false);
            AssertClearance(GetContrabandVerb(entMan, departmentUser, target), true);
            AssertClearance(GetContrabandVerb(entMan, jobUser, target), true);
            AssertClearance(GetContrabandVerb(entMan, mismatchedUser, target), false);
        });
    }

    private static ExamineVerb GetContrabandVerb(IEntityManager entMan, EntityUid user, EntityUid target)
    {
        var ev = new GetVerbsEvent<ExamineVerb>(
            user,
            target,
            @using: null,
            hands: null,
            canInteract: true,
            canComplexInteract: true,
            canAccess: true,
            extraCategories: []);
        entMan.EventBus.RaiseLocalEvent(target, ev);

        Assert.That(ev.Verbs.Count(verb => verb.Text == "Legality"), Is.EqualTo(1));
        return ev.Verbs.Single(verb => verb.Text == "Legality");
    }

    private static void AssertClearance(ExamineVerb verb, bool authorized)
    {
        var expectedText = authorized ? "in the clear" : "avoid visibly carrying";
        var expectedIcon = authorized
            ? "/Textures/Interface/VerbIcons/unlock-green.svg.192dpi.png"
            : "/Textures/Interface/VerbIcons/lock-red.svg.192dpi.png";

        Assert.That(verb.Message, Does.Contain(expectedText));
        Assert.That(verb.Icon, Is.EqualTo(new SpriteSpecifier.Texture(new ResPath(expectedIcon))));
    }
}
