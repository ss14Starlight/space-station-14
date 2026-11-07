using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.Speech;
using Content.Shared.Speech;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Speech;

[TestOf(typeof(AccentGetEvent))]
public sealed class AccentRobustnessTest : GameTest
{
    private static readonly string[] _messages =
    [
        "",
        " ",
        "!",
        "...",
        "a",
        "I",
        "OK",
        "Hello there, how are you doing today?",
        "HELP! THE CAPTAIN IS DEAD!!",
        "1234 5678",
        "ok. ok. ok.",
        "\"quoted\" 'text'",
        "emoji 🙂 here",
        new string('a', 1000),
    ];

    private static readonly HashSet<string> _needsConfiguration =
    [
        "ReplacementAccentComponent", // required `accent` id
    ];

    [Test]
    public async Task AccentsHandleEdgeCaseMessages()
    {
        var factory = Server.ResolveDependency<IComponentFactory>();
        var accents = factory.AllRegisteredTypes
            .Where(t => t.Name.EndsWith("AccentComponent") && !_needsConfiguration.Contains(t.Name))
            .OrderBy(t => t.Name)
            .ToList();

        Assert.That(accents, Is.Not.Empty);

        var failures = new List<string>();

        await Server.WaitPost(() =>
        {
            foreach (var accent in accents)
            {
                var uid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                var comp = (Component) factory.GetComponent(accent);
                SEntMan.AddComponent(uid, comp);

                foreach (var text in _messages)
                {
                    try
                    {
                        var ev = new AccentGetEvent(uid, (SpeechMessage) text);
                        SEntMan.EventBus.RaiseLocalEvent(uid, ev, true);

                        if (ev.Message?.Text == null)
                            failures.Add($"{accent.Name}: returned no text for \"{Shorten(text)}\"");
                    }
                    catch (Exception e)
                    {
                        failures.Add($"{accent.Name}: {e.GetType().Name} on \"{Shorten(text)}\": {e.Message}");
                    }
                }

                SEntMan.DeleteEntity(uid);
            }
        });

        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }

    private static string Shorten(string text) => text.Length > 20 ? text[..20] + "..." : text;
}
