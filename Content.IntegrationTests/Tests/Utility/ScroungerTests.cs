using System.Linq;
using Content.IntegrationTests.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Utility;

[TestOf(typeof(GameDataScrounger))]
public sealed class ScroungerTests
{
    [Test]
    [Description("Checks that the data scrounger finds prototypes, entities, and files.")] // Starlight
    public void ScroungeContentData() // Starlight
    {
        // Starlight - start
        var scrounged = GameDataScrounger.PrototypesOfKind<EntityPrototype>();
        var items = GameDataScrounger.EntitiesWithComponent("Item");
        var yamlFiles = GameDataScrounger.FilesInDirectory("/", "*.yml");
        var textFiles = GameDataScrounger.FilesInDirectory("/", "*.txt");
        var vfsFiles = GameDataScrounger.FilesInDirectoryInVfs("/Maps", "*.yml");

        Assert.Multiple(() =>
        {
            Assert.That(scrounged, Is.Not.Empty);
            Assert.That(items, Is.Not.Empty);
            Assert.That(yamlFiles, Is.Not.Empty);
            Assert.That(textFiles, Is.Not.Empty);
            Assert.That(vfsFiles, Is.Not.Empty);

            if (vfsFiles.Any())
            {
                Assert.That(vfsFiles[0].IsRooted, Is.True);
                Assert.That(vfsFiles[0].ToString(), Does.StartWith("/Maps/"));
            }
        });
        // Starlight - end
    }
}
