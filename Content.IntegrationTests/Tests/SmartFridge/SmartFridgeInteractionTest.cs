using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.SmartFridge;

namespace Content.IntegrationTests.Tests.SmartFridge;

public sealed class SmartFridgeInteractionTest : InteractionTest
{
    private const string SmartFridgeProtoId = "SmartFridge";
    private const string SampleItemProtoId = "FoodAmbrosiaVulgaris";
    private const string SampleDumpableAndInsertableId = "PillCanisterSomething";
    private const int SampleDumpableCount = 5;
    private const string SampleDumpableId = "ChemBagSomething";

    [TestPrototypes]
    private const string TestPrototypes = $@"
- type: entity
  parent: PillCanister
  id: {SampleDumpableAndInsertableId}
  components:
  - type: EntityTableContainerFill
    containers:
      storagebase:
        id: PillCopper
        amount: 5

- type: entity
  parent: ChemBag
  id: {SampleDumpableId}
  components:
  - type: EntityTableContainerFill
    containers:
      storagebase:
        id: PillCopper
        amount: 5
";

    [Test]
    public async Task InsertAndDispenseItemTest()
    {
        await SpawnTarget(SmartFridgeProtoId);
        var fridge = SEntMan.GetEntity(Target.Value);
        var component = SEntMan.GetComponent<SmartFridgeComponent>(fridge);

        await SpawnEntity("APCBasic", SEntMan.GetCoordinates(TargetCoords));
        await RunTicks(1);

        // smartfridge spawns with nothing
        Assert.That(component.Entries, Is.Empty);
        await InteractUsing(SampleItemProtoId);

        // smartfridge now has items
        Assert.That(component.Entries, Is.Not.Empty);
        Assert.That(component.ContainedEntries[component.Entries[0]], Is.Not.Empty);

        // open the UI
        await Activate();
        Assert.That(IsUiOpen(SmartFridgeUiKey.Key));

        // dispense an item
        await SendBui(SmartFridgeUiKey.Key, new SmartFridgeDispenseItemMessage(component.Entries[0]));

        // assert that the listing is still there
        Assert.That(component.Entries, Is.Not.Empty);
        // but empty
        Assert.That(component.ContainedEntries[component.Entries[0]], Is.Empty);

        // and that the thing we dispensed is actually around
        await AssertEntityLookup(
            ("APCBasic", 1),
            (SampleItemProtoId, 1)
        );
    }

    #region Starlight
    [Test]
    public async Task InsertDumpableItemTypesTest()
    {
        // Starlight - start
        await SpawnTarget(SmartFridgeProtoId);
        var fridge = SEntMan.GetEntity(Target.Value);
        var component = SEntMan.GetComponent<SmartFridgeComponent>(fridge);

        await SpawnEntity("APCBasic", SEntMan.GetCoordinates(TargetCoords));
        await RunTicks(1);

        await InteractUsing(SampleDumpableAndInsertableId);

        // The insertable container is stored as one item.
        Assert.That(component.Entries, Is.Not.Empty);
        Assert.That(component.ContainedEntries[component.Entries[0]], Has.Count.EqualTo(1));

        await InteractUsing(SampleDumpableId);

        // The dumpable container contributes its contents as a separate entry.
        Assert.That(component.Entries, Has.Count.EqualTo(2));
        var itemCounts = component.Entries
            .Select(entry => component.ContainedEntries[entry].Count)
            .OrderBy(count => count)
            .ToArray();
        Assert.That(itemCounts, Is.EqualTo(new[] { 1, SampleDumpableCount }));
        // Starlight - end
    }
    #endregion
}
