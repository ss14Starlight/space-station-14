#nullable enable
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Content.Client.CharacterInfo;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.Tests._Starlight;

public sealed class ChatHighlightTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly IConfigurationManager _configManager = null!;
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _uiManager = null!;

    private void InvokeOnCharacterUpdated(ChatUIController chatController, CharacterInfoSystem.CharacterData characterData)
    {
        var method = chatController.GetType().GetMethod(
            "OnCharacterUpdated",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.That(method, Is.Not.Null);

        // Set internal state to allow character update processing
        var attachField = chatController.GetType().GetField(
            "_charInfoIsAttach",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(attachField, Is.Not.Null);
        attachField.SetValue(chatController, true);

        // Invoke update
        method.Invoke(chatController, new object[] { characterData });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public async Task TestCustomHighlightsPreserved()
    {
        var chatController = _uiManager.GetUIController<ChatUIController>();

        // Assert that the CVar defaults to true on Starlight
        Assert.That(_configManager.GetCVar(CCVars.ChatAutoFillHighlights), Is.True);

        // 1. Enable auto-fill highlights
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, true);

        // 2. Set custom highlights
        var customHighlights = "ling\nrev";
        chatController.UpdateHighlights(customHighlights);

        // Verify they are saved
        Assert.That(_configManager.GetCVar(CCVars.ChatHighlights), Is.EqualTo(customHighlights));

        // 3. Simulate character update
        var characterData = new CharacterInfoSystem.CharacterData(
            default,
            "Captain",
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            "John Doe"
        );

        InvokeOnCharacterUpdated(chatController, characterData);

        // 4. Assertions:
        // - Custom highlights in config must remain unchanged
        Assert.That(_configManager.GetCVar(CCVars.ChatHighlights), Is.EqualTo(customHighlights));

        // - Internal active regex highlights must contain both custom & auto-filled highlights
        var highlightsField = chatController.GetType().GetField(
            "_highlights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(highlightsField, Is.Not.Null);
        var activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;

        // Check that custom and auto highlights are loaded
        // Custom:
        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        // Auto:
        Assert.That(activeHighlights, Contains.Item("Captain"));
        Assert.That(activeHighlights, Contains.Item("(?<!\\w)Cap(?!\\w)")); // "Cap" becomes regex-escaped and word-bounded
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)John\ Doe(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)John(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Doe(?!\w)"));

        // 5. Disable auto-fill highlights and verify auto-filled highlights are removed
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, false);

        activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;
        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        Assert.That(activeHighlights, Is.Not.Contains("Captain"));
        Assert.That(activeHighlights, Is.Not.Contains("(?<!\\w)Cap(?!\\w)"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public async Task TestEnablingAutoFillPreservesCustomHighlights()
    {
        var chatController = _uiManager.GetUIController<ChatUIController>();

        // Assert that the CVar defaults to true on Starlight
        Assert.That(_configManager.GetCVar(CCVars.ChatAutoFillHighlights), Is.True);

        // 1. Start with auto-fill disabled
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, false);

        // 2. Set custom highlights
        var customHighlights = "ling\nrev";
        chatController.UpdateHighlights(customHighlights);

        // Verify active matches are ONLY custom highlights
        var highlightsField = chatController.GetType().GetField(
            "_highlights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(highlightsField, Is.Not.Null);
        var activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;

        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        Assert.That(activeHighlights.Count, Is.EqualTo(2));

        // 3. Enable auto-fill highlights
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, true);

        // 4. Simulate character update (spawning into round)
        var characterData = new CharacterInfoSystem.CharacterData(
            default,
            "Captain",
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            "John Doe"
        );

        InvokeOnCharacterUpdated(chatController, characterData);

        // 5. Assertions:
        // - Config highlights MUST NOT be wiped and remain as custom highlights
        Assert.That(_configManager.GetCVar(CCVars.ChatHighlights), Is.EqualTo(customHighlights));

        // - Active highlights list must now merge both custom and auto-filled ones
        activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;
        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        Assert.That(activeHighlights, Contains.Item("Captain"));
        Assert.That(activeHighlights, Contains.Item("(?<!\\w)Cap(?!\\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)John\ Doe(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)John(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Doe(?!\w)"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public async Task TestBorgNameHighlights()
    {
        var chatController = _uiManager.GetUIController<ChatUIController>();
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, true);

        var characterData = new CharacterInfoSystem.CharacterData(
            default,
            "Cyborg",
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            "C-3-D2 (Si-8545)"
        );

        InvokeOnCharacterUpdated(chatController, characterData);

        var highlightsField = chatController.GetType().GetField(
            "_highlights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(highlightsField, Is.Not.Null);
        var activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;

        // Verify that the full clean name "C-3-D2" and the last part "D2" are highlighted as whole words
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)C-3-D2(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)D2(?!\w)"));

        // Verify that the single character parts ("C", "3") and the parenthesized suffix are NOT highlighted
        foreach (var highlight in activeHighlights)
        {
            Assert.That(highlight, Is.Not.Contains("(?<!\\w)C(?!\\w)"));
            Assert.That(highlight, Is.Not.Contains("(?<!\\w)3(?!\\w)"));
            Assert.That(highlight, Is.Not.Contains("Si-8545"));
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public async Task TestSpeciesNameHighlightsAvali()
    {
        var chatController = _uiManager.GetUIController<ChatUIController>();
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, true);

        var highlightsField = chatController.GetType().GetField(
            "_highlights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(highlightsField, Is.Not.Null);

        // 1. Avali Name (comma-separated pack name)
        var avaliData = new CharacterInfoSystem.CharacterData(
            default,
            "Avali",
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            "Bird, Testdev Pack"
        );
        InvokeOnCharacterUpdated(chatController, avaliData);
        var activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Bird,\ Testdev\ Pack(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Bird(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Testdev\ Pack(?!\w)"));

        // 1b. Eats-Food, Clan (Avali name with single hyphen first part)
        var eatingData = new CharacterInfoSystem.CharacterData(
            default,
            "Avali",
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            "Eats-Food, Clan"
        );
        InvokeOnCharacterUpdated(chatController, eatingData);
        activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Eats-Food,\ Clan(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Eats-Food(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Clan(?!\w)"));
        // Hyphen parts of the comma-based components must NOT be split further (e.g. "Eats" and "Food" must not be in activeHighlights)
        foreach (var highlight in activeHighlights)
        {
            Assert.That(highlight, Is.Not.EqualTo(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Eats(?!\w)"));
            Assert.That(highlight, Is.Not.EqualTo(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Food(?!\w)"));
        }

        // 1c. Alpha-Beta-Gamma-Delta, Clan (Avali name with multi-hyphen first part)
        var aibcData = new CharacterInfoSystem.CharacterData(
            default,
            "Avali",
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            "Alpha-Beta-Gamma-Delta, Clan"
        );
        InvokeOnCharacterUpdated(chatController, aibcData);
        activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Alpha-Beta-Gamma-Delta,\ Clan(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Alpha-Beta-Gamma-Delta(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Clan(?!\w)"));
        // Hyphen parts must NOT be split further
        foreach (var highlight in activeHighlights)
        {
            Assert.That(highlight, Is.Not.EqualTo(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Alpha(?!\w)"));
            Assert.That(highlight, Is.Not.EqualTo(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Beta(?!\w)"));
            Assert.That(highlight, Is.Not.EqualTo(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Gamma(?!\w)"));
            Assert.That(highlight, Is.Not.EqualTo(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Delta(?!\w)"));
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public async Task TestSpeciesNameHighlightsLizard()
    {
        var chatController = _uiManager.GetUIController<ChatUIController>();
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, true);

        var highlightsField = chatController.GetType().GetField(
            "_highlights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(highlightsField, Is.Not.Null);

        // 2. Lizard Name (multiple hyphens - first and last parts are split, middle connector "The" is ignored)
        var lizardData = new CharacterInfoSystem.CharacterData(
            default,
            "Reptilian",
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            "Eats-The-Food"
        );
        InvokeOnCharacterUpdated(chatController, lizardData);
        var activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Eats-The-Food(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Eats(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Food(?!\w)"));
        foreach (var highlight in activeHighlights)
        {
            Assert.That(highlight, Is.Not.Contains("(?<!\\w)The(?!\\w)"));
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public async Task TestSpeciesNameHighlightsParentheses()
    {
        var chatController = _uiManager.GetUIController<ChatUIController>();
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, true);

        var highlightsField = chatController.GetType().GetField(
            "_highlights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(highlightsField, Is.Not.Null);

        // 3. Parentheses removal (e.g. John Doe (Ghost))
        var ghostData = new CharacterInfoSystem.CharacterData(
            default,
            "Human",
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            "John Doe (Ghost)"
        );
        InvokeOnCharacterUpdated(chatController, ghostData);
        var activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)John\ Doe(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)John(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Doe(?!\w)"));
        foreach (var highlight in activeHighlights)
        {
            Assert.That(highlight, Is.Not.Contains("Ghost"));
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public async Task TestSpeciesNameHighlightsQuotes()
    {
        var chatController = _uiManager.GetUIController<ChatUIController>();
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, true);

        var highlightsField = chatController.GetType().GetField(
            "_highlights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(highlightsField, Is.Not.Null);

        // 4. Quotes handling (e.g. John \"Johnny\" Doe)
        var quoteData = new CharacterInfoSystem.CharacterData(
            default,
            "Human",
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            "John \"Johnny\" Doe"
        );
        InvokeOnCharacterUpdated(chatController, quoteData);
        var activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)John\ ""Johnny""\ Doe(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)John(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Johnny(?!\w)"));
        Assert.That(activeHighlights, Contains.Item(@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))(?<!\w)Doe(?!\w)"));
    }
}
