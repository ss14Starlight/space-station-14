using Content.Client.Guidebook;
using Content.Client.Guidebook.Richtext;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Shared.Guidebook;

namespace Content.IntegrationTests.Tests.Guidebook;

[TestFixture]
[TestOf(typeof(GuidebookSystem))]
[TestOf(typeof(GuideEntryPrototype))]
[TestOf(typeof(DocumentParsingManager))]
public sealed class GuideEntryPrototypeTests : GameTest
{
    [Test]
    public async Task ValidatePrototypeContents()
    {
        var pair = Pair;
        var client = pair.Client;
        await client.WaitIdleAsync();
        var protoMan = client.ResolveDependency<IPrototypeManager>();
        var resMan = client.ResolveDependency<IResourceManager>();
        var parser = client.ResolveDependency<DocumentParsingManager>();
        var prototypes = protoMan.EnumeratePrototypes<GuideEntryPrototype>().ToList();

        foreach (var proto in prototypes)
        {
            await client.WaitAssertion(() =>
            {
                using var reader = resMan.ContentFileReadText(proto.Text);
                var text = reader.ReadToEnd();
                Assert.That(parser.TryAddMarkup(new Document(), text), $"Failed to parse guidebook: {proto.Id}");
            });

            // Avoid styleguide update limit
            await client.WaitRunTicks(1);
        }
    }
}
