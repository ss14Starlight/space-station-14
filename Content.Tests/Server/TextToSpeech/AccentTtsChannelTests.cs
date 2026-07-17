using Content.Server._Starlight.Speech.EntitySystems;
using Content.Server.Speech.EntitySystems;
using Content.Shared._Starlight.Speech;
using Content.Tests;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.UnitTesting;
using System.IO;

namespace Content.Tests.Server.TextToSpeech;

/// <summary>
/// Ensures accent systems keep pronunciation-hurting transforms off Message.Tts,
/// while whole-word replacement accents still update the TTS channel.
/// </summary>
[TestFixture]
public sealed class AccentTtsChannelTests : ContentUnitTest
{
    public override UnitTestProject Project => UnitTestProject.Server;

    [Test]
    public void OwOAccent_DoesNotMutateTts()
    {
        var system = new OwOAccentSystem();
        SpeechMessage message = "hello please look now you are cute";
        var ttsBefore = message.Tts;

        system.Accentuate(message);

        Assert.That(message.Tts, Is.EqualTo(ttsBefore));
        Assert.That(message.Text, Does.Contain("mew"));
        Assert.That(message.Text, Does.Contain("meow"));
        Assert.That(message.Text, Does.Contain("wu"));
        Assert.That(message.Text, Does.Contain("pwez")); // please → plez → pwez
    }

    [Test]
    public void BarkAccent_DoesNotMutateTts()
    {
        var system = new BarkAccentSystem();
        IoCManager.InjectDependencies(system);

        SpeechMessage message = "ah oh hello!";
        var ttsBefore = message.Tts;

        system.Accentuate(message);

        Assert.That(message.Tts, Is.EqualTo(ttsBefore));
        Assert.That(message.Text, Does.Contain("arf"));
        Assert.That(message.Text, Does.Contain("Woof").Or.Contain("WOOF").Or.Contain("wof"));
    }

    [Test]
    public void ReplacementAccent_FallsBackToWordReplacementsForTts()
    {
        IoCManager.Resolve<ISerializationManager>().Initialize();
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        prototypes.Initialize();
        prototypes.LoadFromStream(new StringReader("""
            - type: accent
              id: test-tts-word-fallback
              wordReplacements:
                solar: photovoltaic
            """));
        prototypes.ResolveResults();

        var system = new ReplacementAccentSystem();
        IoCManager.InjectDependencies(system);

        SpeechMessage message = "the solar panels";
        message = system.ApplyReplacements(message, "test-tts-word-fallback");

        Assert.That(message.Text, Does.Contain("photovoltaic"));
        Assert.That(message.Tts, Does.Contain("photovoltaic"));
        Assert.That(message.Tts, Does.Not.Contain("solar"));
    }
}
