using System.Linq;
using System.Text.RegularExpressions;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Content.Shared.CCVar;
using Content.Client.CharacterInfo;
using Content.Client._Starlight.TextToSpeech;
using static Content.Client.CharacterInfo.CharacterInfoSystem;

namespace Content.Client.UserInterface.Systems.Chat;

/// <summary>
/// A partial class of ChatUIController that handles the saving and loading of highlights for the chatbox.
/// It also makes use of the CharacterInfoSystem to optionally generate highlights based on the character's info.
/// </summary>
public sealed partial class ChatUIController : IOnSystemChanged<CharacterInfoSystem>
{
    [Dependency] private ILocalizationManager _loc = default!;
    [UISystemDependency] private readonly CharacterInfoSystem _characterInfo = default!;

    private static readonly Regex StartDoubleQuote = new("\"$");
    private static readonly Regex EndDoubleQuote = new("^\"|(?<=^@)\"");
    private static readonly Regex StartAtSign = new("^@");

    /// <summary>
    ///     The list of words to be highlighted in the chatbox.
    /// </summary>
    private readonly List<string> _highlights = new();

    /// <summary>
    ///     The string holding the hex color used to highlight words.
    /// </summary>
    private string? _highlightsColor;

    private bool _autoFillHighlightsEnabled;
    private string _autoHighlights = ""; // Starlight
    private CharacterData? _cachedCharacterData; // Starlight

    /// <summary>
    ///     The boolean that keeps track of the 'OnCharacterUpdated' event, whenever it's a player attaching or opening the character info panel.
    /// </summary>
    private bool _charInfoIsAttach = false;

    public event Action<string>? HighlightsUpdated;
    // Starlight Start
    /// <summary>
    ///     Event triggered when the auto-fill highlights list is updated.
    /// </summary>
    public event Action<string>? AutoHighlightsUpdated;

    /// <summary>
    ///     The current active auto-fill highlights list, or empty if disabled.
    /// </summary>
    public string AutoHighlights => _autoFillHighlightsEnabled ? _autoHighlights : string.Empty;
    // Starlight End

    private void InitializeHighlights()
    {
        // Starlight Start
        _config.OnValueChanged(CCVars.ChatAutoFillHighlights, (value) =>
        {
            _autoFillHighlightsEnabled = value;
            if (value)
                UpdateAutoFillHighlights();
            else
            {
                ReloadHighlights();
                AutoHighlightsUpdated?.Invoke(AutoHighlights);
            }
        }, true);
        // Starlight End

        _config.OnValueChanged(CCVars.ChatHighlightsColor, (value) => { _highlightsColor = value; }, true);

        // Load highlights if any were saved.
        var highlights = _config.GetCVar(CCVars.ChatHighlights);

        if (!string.IsNullOrEmpty(highlights))
        {
            UpdateHighlights(highlights, true);
        }
    }

    public void OnSystemLoaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate += OnCharacterUpdated;
    }

    public void OnSystemUnloaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate -= OnCharacterUpdated;
    }

    private void UpdateAutoFillHighlights()
    {
        if (!_autoFillHighlightsEnabled)
            return;

        // Starlight start
        _autoHighlights = string.Empty;
        ReloadHighlights();
        AutoHighlightsUpdated?.Invoke(AutoHighlights);

        if (_cachedCharacterData != null && _cachedCharacterData.Value.Entity == _player.LocalEntity)
        {
            _charInfoIsAttach = true;
            OnCharacterUpdated(_cachedCharacterData.Value);
        }
        else
        {
            // If auto highlights are enabled generate a request for new character info
            // that will be used to determine the highlights.
            _charInfoIsAttach = true;
            _characterInfo?.RequestCharacterInfo();
        }
        // Starlight end
    }

    // Starlight Start
    public void UpdateHighlights(string newHighlights, bool firstLoad = false)
    {
        // Do nothing if the provided highlights are the same as the old ones and it is not the first time.
        if (!firstLoad && _config.GetCVar(CCVars.ChatHighlights).Equals(newHighlights, StringComparison.CurrentCultureIgnoreCase))
            return;

        _config.SetCVar(CCVars.ChatHighlights, newHighlights);
        _config.SaveToFile();

        ReloadHighlights();
        HighlightsUpdated?.Invoke(newHighlights);
    }

    public void ReloadHighlights()
    {
        _highlights.Clear();

        var combined = _config.GetCVar(CCVars.ChatHighlights);
        if (_autoFillHighlightsEnabled && !string.IsNullOrEmpty(_autoHighlights))
        {
            if (string.IsNullOrEmpty(combined))
                combined = _autoHighlights;
            else
                combined += "\n" + _autoHighlights;
        }

        // We first subdivide the highlights based on newlines to prevent replacing
        // a valid "\n" tag and adding it to the final regex.
        var splittedHighlights = combined.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < splittedHighlights.Length; i++)
        {
            // Replace every "\" character with a "\\" to prevent "\n", "\0", etc...
            var keyword = splittedHighlights[i].Replace(@"\", @"\\");

            // Escape the keyword to prevent special characters like "(" and ")" to be considered valid regex.
            keyword = Regex.Escape(keyword);

            // 1. Since the "["s in WrappedMessage are already sanitized, add 2 extra "\"s
            // to make sure it matches the literal "\" before the square bracket.
            keyword = keyword.Replace(@"\[", @"\\\[");

            // If present, replace the double quotes at the edges with tags
            // that make sure the words to match are separated by spaces or punctuation.
            // NOTE: The reason why we don't use \b tags is that \b doesn't match reverse slash characters "\" so
            // a pre-sanitized (see 1.) string like "\[test]" wouldn't get picked up by the \b.
            if (keyword.Any(c => c == '"'))
            {
                // Matches the last double quote character.
                keyword = StartDoubleQuote.Replace(keyword, "(?!\\w)");
                // When matching for the first double quote character we also consider the possibility
                // of the double quote being preceded by a @ character.
                keyword = EndDoubleQuote.Replace(keyword, "(?<!\\w)");
            }

            // Make sure the character's name is highlighted only when mentioned directly (eg. it's said by someone),
            // for example in 'Name Surname says, "..."' 'Name Surname' won't be highlighted.
            keyword = StartAtSign.Replace(keyword, @"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*"".*)|(?<=\n.*))");

            _highlights.Add(keyword);
        }

        // Arrange the list of highlights in descending order so that when highlighting,
        // the full word (eg. "Security") gets picked before the abbreviation (eg. "Sec").
        _highlights.Sort((x, y) => y.Length.CompareTo(x.Length));
    }
    // Starlight End

    private void OnCharacterUpdated(CharacterData data)
    {
        _cachedCharacterData = data; // Starlight

        // If _charInfoIsAttach is false then the opening of the character panel was the one
        // to generate the event, dismiss it.
        if (!_charInfoIsAttach)
            return;

        var (_, job, _, _, entityName) = data;

        // Mark this entity's name as our character name for the "UpdateHighlights" function.
        var newHighlights = "@" + entityName;

        // Subdivide the character's name based on spaces or hyphens so that every word gets highlighted.
        if (newHighlights.Count(c => (c == ' ' || c == '-')) == 1)
            newHighlights = newHighlights.Replace("-", "\n@").Replace(" ", "\n@");

        // If the character has a name with more than one hyphen assume it is a lizard name and extract the first and
        // last name eg. "Eats-The-Food" -> "@Eats" "@Food"
        if (newHighlights.Count(c => c == '-') > 1)
            newHighlights = newHighlights.Split('-')[0] + "\n@" + newHighlights.Split('-')[^1];

        //Starlight begin
        // If the character has a name with a single comma, assume it is an Avali name and extract the name and
        // pack name eg. "Bird, Testdev Pack" -> "@Bird" "@Testdev Pack"
        if (newHighlights.Count(c => c == ',') == 1)
            newHighlights = newHighlights.Split(',')[0] + "\n@" + newHighlights.Split(',')[1].TrimStart(' ');
        //Starlight end

        // Convert the job title to kebab-case and use it as a key for the loc file.
        var jobKey = job.Replace(' ', '-').ToLower();

        if (_loc.TryGetString($"highlights-{jobKey}", out var jobMatches))
            newHighlights += '\n' + jobMatches.Replace(", ", "\n");

        // Starlight Start
        _autoHighlights = newHighlights;
        ReloadHighlights();
        AutoHighlightsUpdated?.Invoke(AutoHighlights);
        // Starlight End
        _charInfoIsAttach = false;
    }

    // Starlight start
    /// <summary>
    ///     Clears the active TTS speech queue.
    /// </summary>
    public void ClearTTSQueue()
    {
        if (_ent.TrySystem<TextToSpeechSystem>(out var tts))
            tts.ClearQueue();
    }

    /// <summary>
    ///     Sets the mute state of a TTS radio channel.
    /// </summary>
    public void SetTTSChannelMuted(Robust.Shared.Prototypes.ProtoId<Content.Shared.Radio.RadioChannelPrototype> channelId, bool muted)
    {
        if (_ent.TrySystem<TextToSpeechStreamSystem>(out var ttsStream))
            ttsStream.SetChannelMuted(channelId, muted);
    }
    // Starlight end
}
