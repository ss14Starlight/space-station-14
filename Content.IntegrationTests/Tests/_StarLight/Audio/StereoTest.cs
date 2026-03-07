using System.Collections.Generic;
using System.Linq;
using Content.Shared.Audio;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Starlight.Audio;

[TestFixture]
public sealed class StereoTest
{
    // Mark specific files as ignored if they not using positioning, for example: Ambience, Announcements.
    public List<ResPath> IgnoredFiles = [
            new ResPath("/Audio/Effects/explosionfar.ogg"), // Global
            new ResPath("/Audio/Effects/explosionsmallfar.ogg"), // Global
            new ResPath("/Audio/Effects/eye_close.ogg"), // Global
            new ResPath("/Audio/Effects/eye_open.ogg"), // Global
            new ResPath("/Audio/Effects/newplayerping.ogg"), // Global
            new ResPath("/Audio/Effects/voteding.ogg"), // Global
            new ResPath("/Audio/Misc/bluealert.ogg"), // Global
            new ResPath("/Audio/Misc/delta.ogg"), // Global
            new ResPath("/Audio/Misc/delta_alt.ogg"), // Global
            new ResPath("/Audio/Misc/epsilon.ogg"), // Global
            new ResPath("/Audio/Misc/gamma.ogg"), // Global
            new ResPath("/Audio/Misc/redalert.ogg"), // Global
            new ResPath("/Audio/Misc/narsie_rises.ogg"), // Global
            new ResPath("/Audio/Misc/ninja_greeting.ogg"), // Global
            new ResPath("/Audio/Misc/paradox_clone_greeting.ogg"), // Global
            new ResPath("/Audio/Misc/ratvar_reveal.ogg"), // Global
            new ResPath("/Audio/Misc/ratvar_rises.ogg"), // Global
            new ResPath("/Audio/Mecha/powerup.ogg"), // Global
            new ResPath("/Audio/Mecha/skyfall_power_up.ogg"), // Global
            new ResPath("/Audio/_Starlight/Admeme/announcement_horror.ogg"), // Global
            new ResPath("/Audio/_Starlight/Effects/sov_choir_global.ogg"), // Global
            new ResPath("/Audio/_Starlight/Misc/bluealert.ogg"), // Global
            new ResPath("/Audio/_Starlight/Misc/omega.ogg"), // Global
            new ResPath("/Audio/_Starlight/Misc/omega_alt.ogg"), // Global
            new ResPath("/Audio/_Starlight/Misc/orange.ogg"), // Global
            new ResPath("/Audio/_Starlight/Misc/redalert.ogg"), // Global
            new ResPath("/Audio/_Starlight/Misc/rev_end.ogg"), // Global
            new ResPath("/Audio/_Starlight/Misc/sov_win.ogg"), // Global
            new ResPath("/Audio/_Starlight/Thaven/moods_changed.ogg"), // Global
            new ResPath("/Audio/_Starlight/Effects/vampire/sound_hallucinations_im_here1.ogg"), // Global
        ];

    public List<ResPath> IgnoredPaths = [
            new ResPath("/Audio/Announcements"), // Announcements can be stereo because they don't have positioning.
            new ResPath("/Audio/_Starlight/Announcements/"),
            new ResPath("/Audio/Expedition"),
            new ResPath("/Audio/Lobby"),
            new ResPath("/Audio/_Starlight/Lobby"),
            new ResPath("/Audio/Effects/Weather/"),
            new ResPath("/Audio/StationEvents/"),
            new ResPath("/Audio/_Starlight/StationEvents/"),
            new ResPath("/Audio/Ambience/Antag/"),
            new ResPath("/Audio/_Starlight/Ambience/Antag/"),
            new ResPath("/Audio/_Starlight/Effects/Radio/"),
        ];

    [Test]
    public async Task TestAudioFiles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        var resMan = client.ResolveDependency<IResourceManager>();
        var protoMan = client.ResolveDependency<IPrototypeManager>();
        var audioMan = client.ResolveDependency<IAudioManager>();

        var audioRoot = new ResPath("/Audio/");

        var badFiles = new Dictionary<string, string>();

        var ambienceTracks = new List<ResPath>();
        foreach (var ambience in protoMan.EnumeratePrototypes<AmbientMusicPrototype>())
        {
            switch (ambience.Sound)
            {
                case SoundCollectionSpecifier collection:
                    if (collection.Collection == null)
                        break;

                    var slothCud = protoMan.Index<SoundCollectionPrototype>(collection.Collection);
                    ambienceTracks.AddRange(slothCud.PickFiles);
                    break;
                case SoundPathSpecifier path:
                    ambienceTracks.Add(path.Path);
                    break;
            }
        }

        foreach (var file in resMan.ContentFindFiles(audioRoot))
        {
            if (ambienceTracks.Contains(file))
                continue; // Ambience tracks can be stereo, so we skip them.

            // We can ignore some files/paths if we want to, for example if they are stereo on purpose or if we just don't care about them.
            if (IgnoredFiles.Contains(file) || IgnoredPaths.Any(p => file.ToString().StartsWith(p.ToString())))
                continue;

            var ext = file.Extension.ToLowerInvariant();
            if (ext is not "ogg" and not "wav")
                continue;

            try
            {
                using var stream = resMan.ContentFileRead(file);
                using var audioStream = ext == "ogg" ? audioMan.LoadAudioOggVorbis(stream) : audioMan.LoadAudioWav(stream);
                if (audioStream.ChannelCount != 1)
                {
                    badFiles[file.ToString()] = audioStream.ChannelCount == 2
                        ? $"This audio is STEREO but NEEDS to be MONO!"
                        : $"Incorrect channels count! Channel count: {audioStream.ChannelCount}, but it should have only 1 channel.";
                }
            }
            catch (Exception e)
            {
                Assert.Fail($"Failed to read audio file {file}: {e}");
            }
        }

        Assert.That(badFiles, Is.Empty, "Some audio is invalid:\n" + string.Join('\n', badFiles.Select(p => $"{p.Key}: {p.Value}"))
        );

        await pair.CleanReturnAsync();
    }
}