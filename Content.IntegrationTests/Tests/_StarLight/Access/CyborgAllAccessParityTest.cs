using System.Collections.Generic;
using System.Linq;
using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._StarLight.Access;

public sealed class CyborgAllAccessParityTest
{

    /// <summary>
    /// Accesses that AllAccess has but CyborgAllAccess does not.
    /// </summary>
    private static readonly HashSet<ProtoId<AccessLevelPrototype>> _exclusions =
    [
        new("Debrief") // Borgs are not head of staff, and should not be in debrief.
    ];

    private static readonly ProtoId<AccessGroupPrototype> _allAccessGroup = "AllAccess";
    private static readonly ProtoId<AccessGroupPrototype> _cyborgAllAccessGroup = "CyborgAllAccess";

    /// <summary>
    /// Asserts that CyborgAllAccess = AllAccess - Exclusions.
    /// </summary>
    [Test]
    public async Task CyborgAllAccessEqualsAllAccessMinusExclusions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var allAccessTags = protoManager.Index(_allAccessGroup).Tags;
            var cyborgAllAccessTags = protoManager.Index(_cyborgAllAccessGroup).Tags;

            var expectedCyborgTags = allAccessTags.Except(_exclusions).ToList();

            var missingFromCyborg = expectedCyborgTags.Except(cyborgAllAccessTags).ToList();
            var extraInCyborg = cyborgAllAccessTags.Except(expectedCyborgTags).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(missingFromCyborg, Is.Empty,
                    $"Tags present in AllAccess but missing from CyborgAllAccess: " +
                    $"[{string.Join(", ", missingFromCyborg)}]. " +
                    $"Either add them to CyborgAllAccess in " +
                    $"Resources/Prototypes/_Starlight/Access/misc.yml, " +
                    $"or add them to {nameof(_exclusions)} in this test " +
                    $"with a comment explaining why borgs should not have the access.");

                Assert.That(extraInCyborg, Is.Empty,
                    $"Tags present in CyborgAllAccess but absent from AllAccess: " +
                    $"[{string.Join(", ", extraInCyborg)}]. " +
                    $"Either add them to AllAccess in " +
                    $"Resources/Prototypes/Access/misc.yml, " +
                    $"or remove them from CyborgAllAccess.");
            }

        });

        await pair.CleanReturnAsync();
    }

}
