using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Language;
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Language.Components;
using Content.Shared._Starlight.Language.Systems;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._Starlight.Language;

[TestOf(typeof(LanguageSystem))]
public sealed class LanguageTest : GameTest
{
    private const string Message = "The quick brown fox jumps over the lazy dog";

    [Test]
    public async Task LanguagePrototypesAreUsable()
    {
        var loc = Server.ResolveDependency<ILocalizationManager>();

        await Server.WaitAssertion(() =>
        {
            var prefixes = new Dictionary<string, string>();

            using (Assert.EnterMultipleScope())
            {
                foreach (var language in SProtoMan.EnumeratePrototypes<LanguagePrototype>())
                {
                    Assert.That(loc.HasString($"language-{language.ID}-name"), $"Language {language.ID} has no name.");
                    Assert.That(loc.HasString($"language-{language.ID}-description"), $"Language {language.ID} has no description.");

                    if (language.ChatPrefix is not { } prefix)
                        continue;

                    Assert.That(prefix.Length, Is.EqualTo(1).Or.EqualTo(3),
                        $"Language {language.ID} has chat prefix '{prefix}', prefixes must be 1 or 3 characters long.");

                    var key = prefix.ToLowerInvariant();
                    if (prefixes.TryGetValue(key, out var other))
                        Assert.Fail($"Languages {other} and {language.ID} share the chat prefix '{prefix}'.");
                    else
                        prefixes[key] = language.ID;
                }
            }
        });
    }

    [Test]
    public async Task ObfuscationIsStableAndHidesText()
    {
        var languages = Server.System<LanguageSystem>();

        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                foreach (var language in SProtoMan.EnumeratePrototypes<LanguagePrototype>())
                {
                    if (language.ID == SharedLanguageSystem.UniversalPrototype)
                        continue;

                    var first = languages.ObfuscateSpeech(Message, language);
                    var second = languages.ObfuscateSpeech(Message, language);

                    Assert.That(first, Is.Empty.Or.Null, $"Language {language.ID} returns Null or Empty after obfuscation!");
                    Assert.That(second, Is.EqualTo(first), $"Language {language.ID} obfuscates the same message differently.");
                    Assert.That(first, Does.Not.Contain(Message).IgnoreCase, $"Language {language.ID} does not hide the message.");
                }
            }
        });
    }

    [Test]
    public async Task RoundStartSpeciesCanSpeak()
    {
        var map = await Pair.CreateTestMap();
        var languages = Server.System<LanguageSystem>();
        var species = SProtoMan.EnumeratePrototypes<SpeciesPrototype>().Where(s => s.RoundStart).ToList();

        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                foreach (var proto in species)
                {
                    var mob = SEntMan.SpawnEntity(proto.Prototype, map.GridCoords);

                    if (!SEntMan.TryGetComponent<LanguageSpeakerComponent>(mob, out var speaker))
                    {
                        Assert.Fail($"Species {proto.ID} ({proto.Prototype}) has no {nameof(LanguageSpeakerComponent)}.");
                        SEntMan.DeleteEntity(mob);
                        continue;
                    }

                    Assert.That(speaker.SpokenLanguages, Is.Not.Empty, $"Species {proto.ID} speaks no languages.");
                    Assert.That(languages.CanSpeak((mob, speaker), speaker.CurrentLanguage),
                        $"Species {proto.ID} starts with '{speaker.CurrentLanguage}', which it cannot speak.");
                    Assert.That(languages.CanUnderstand((mob, speaker), SharedLanguageSystem.FallbackLanguagePrototype),
                        $"Species {proto.ID} cannot understand {SharedLanguageSystem.FallbackLanguagePrototype}.");

                    SEntMan.DeleteEntity(mob);
                }
            }
        });
    }

    [Test]
    public async Task ChatPrefixSelectsLanguage()
    {
        var map = await Pair.CreateTestMap();
        var languages = Server.System<LanguageSystem>();

        await Server.WaitAssertion(() =>
        {
            var mob = SEntMan.SpawnEntity(null, map.GridCoords);
            var withPrefix = SProtoMan.EnumeratePrototypes<LanguagePrototype>().Where(l => l.ChatPrefix != null).ToArray();
            Assert.That(withPrefix.Length, Is.GreaterThan(0), $"Trying to check language prefixes but languages enumaration returned 0!");
            foreach (var language in withPrefix)
                languages.AddLanguage(mob, language.ID);

            var speaker = SEntMan.GetComponent<LanguageSpeakerComponent>(mob);

            using (Assert.EnterMultipleScope())
            {
                foreach (var language in withPrefix)
                {
                    var input = $"{SharedLanguageSystem.ChatPrefixChar}{language.ChatPrefix} hello";
                    var parsed = languages.GetLanguageFromPrefix((mob, speaker), ref input, out var ok, modifyText: true);

                    Assert.That(ok, $"Prefix '{language.ChatPrefix}' of {language.ID} was not recognised.");
                    Assert.That(parsed.ID, Is.EqualTo(language.ID), $"Prefix '{language.ChatPrefix}' picked {parsed.ID} instead of {language.ID}.");
                    Assert.That(input, Is.EqualTo(" hello"), $"Prefix '{language.ChatPrefix}' was not stripped.");
                }
            }

            SEntMan.DeleteEntity(mob);
        });
    }
}
