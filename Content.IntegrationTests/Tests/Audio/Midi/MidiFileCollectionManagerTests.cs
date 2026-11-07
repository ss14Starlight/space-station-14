#nullable enable

using System.IO;
using System.Linq;
using Content.Client.Audio.Midi;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Audio.Midi;

[TestFixture]
public sealed partial class MidiFileCollectionManagerTests : GameTest
{
    private static readonly byte[] TestBytes = [1, 2, 3, 4, 5, 6];
    private static readonly ResPath TestFileName = new ResPath("unit_test.midi");
    private static ResPath TestUserDataDir => new ResPath("/UserMidis/");
    private static ResPath TestFullPath => TestUserDataDir / TestFileName;

    private IResourceManager ResManager => Pair.Client.ResolveDependency<IResourceManager>();
    private MidiFileCollectionManager MidiLibManager => Pair.Client.ResolveDependency<MidiFileCollectionManager>();

    [TearDown]
    public void CleanUserData()
    {
        foreach (var file in ResManager.UserData.DirectoryEntries(TestUserDataDir))
        {
            ResManager.UserData.Delete(new ResPath(TestUserDataDir + file));
        }

        MidiLibManager.ReloadLibrary();
    }

    [Test]
    public async Task TestAddMidiFile()
    {
        var addedFileName = new ResPath("");
        Stream stream = new MemoryStream(TestBytes);
        MidiLibManager.MidiFileAdded += s => { addedFileName = s; };

        await MidiLibManager.AddMidiFile(TestFileName, stream);
        var outputBytes = ResManager.UserData.ReadAllBytes(TestFullPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(MidiLibManager.GetMidiFiles(), Contains.Item(TestFileName));
            Assert.That(outputBytes, Is.EqualTo(TestBytes));
            Assert.That(addedFileName, Is.EqualTo(TestFileName));
        }
    }

    [Test]
    public void TestGetMidiData()
    {
        ResManager.UserData.WriteAllBytes(TestFullPath, TestBytes);
        var midiBytes = MidiLibManager.GetMidiData(TestFileName);

        Assert.That(TestBytes, Is.EqualTo(midiBytes));
    }

    #region Starlight
    [Test]
    public void TestRenameAndRemoveMidiFiles()
    {
        var renamedFileName = new ResPath("unit_test_renamed.midi");
        var removeFileName = new ResPath("unit_test_remove.midi");
        var removeFullPath = TestUserDataDir / removeFileName;
        ResManager.UserData.WriteAllBytes(TestFullPath, TestBytes);
        ResManager.UserData.WriteAllBytes(removeFullPath, TestBytes);
        MidiLibManager.ReloadLibrary();
        Assert.Multiple(() =>
        {
            Assert.That(ResManager.UserData.Exists(TestFullPath), Is.True);
            Assert.That(ResManager.UserData.Exists(removeFullPath), Is.True);
        });

        var removedFileName = new ResPath("");
        var addedFileName = new ResPath("");
        MidiLibManager.MidiFileRemoved += s => { removedFileName = s; };
        MidiLibManager.MidiFileAdded += s => addedFileName = s;

        MidiLibManager.RenameMidiFile(TestFileName, renamedFileName);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ResManager.UserData.Exists(TestUserDataDir / renamedFileName), Is.True);
            Assert.That(ResManager.UserData.Exists(TestFullPath), Is.False);
            Assert.That(removedFileName, Is.EqualTo(TestFileName));
            Assert.That(addedFileName, Is.EqualTo(renamedFileName));
        }

        removedFileName = new ResPath("");
        MidiLibManager.RemoveMidiFile(removeFileName);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ResManager.UserData.Exists(removeFullPath), Is.False);
            Assert.That(MidiLibManager.GetMidiFiles(), Does.Contain(renamedFileName));
            Assert.That(MidiLibManager.GetMidiFiles(), Does.Not.Contain(removeFileName));
            Assert.That(removedFileName, Is.EqualTo(removeFileName));
        }
    }
    #endregion

    [Test]
    public async Task TestRemoveAllMidiFiles()
    {
        var resetFired = false;

        MidiLibManager.MidiFilesReset += () => { resetFired = true; };
        await MidiLibManager.AddMidiFile(new ResPath("1_unit_test.midi"), TestBytes);
        await MidiLibManager.AddMidiFile(new ResPath("2_unit_test.midi"), TestBytes);
        await MidiLibManager.AddMidiFile(new ResPath("3_unit_test.midi"), TestBytes);

        Assert.That(MidiLibManager.GetMidiFiles().Count(), Is.EqualTo(3));

        MidiLibManager.RemoveAllMidiFiles();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MidiLibManager.GetMidiFiles(), Is.Empty);
            Assert.That(resetFired, Is.True);
        }
    }
}
