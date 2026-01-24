using System.Collections.Generic;
using System.Linq;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Starlight.Audio;

[TestFixture]
public sealed class StereoTest
{
    [Test]
    public async Task TestAudioFiles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        var resMan = client.ResolveDependency<IResourceManager>();
        var audioMan = client.ResolveDependency<IAudioManager>();

        var audioRoot = new ResPath("/Audio/");

        var badFiles = new Dictionary<string, string>();

        foreach (var file in resMan.ContentFindFiles(audioRoot))
        {
            var ext = file.Extension.ToLowerInvariant();
            if (ext is not "ogg" and not "wav")
                continue;

            try
            {
                using var stream = resMan.ContentFileRead(file);
                var audioStream = ext == "ogg" ? audioMan.LoadAudioOggVorbis(stream) : audioMan.LoadAudioWav(stream);
                if (audioStream.ChannelCount != 1)
                {
                    if (audioStream.ChannelCount == 2)
                        badFiles[file.ToString()] = ($"This audio is stereo!");
                    else
                        badFiles[file.ToString()] = ($"Incorrect channels count! Channel count: {audioStream.ChannelCount}");
                }
            }
            catch (Exception e)
            {
                Assert.Fail($"Failed to read audio file {file}: {e}");
            }
        }

        Assert.That(badFiles, Is.Empty, "Some audio is invalid:\n" + string.Join('\n', badFiles.Select(p => $"{p.Key}: {p.Value}"))
        );
    }
}