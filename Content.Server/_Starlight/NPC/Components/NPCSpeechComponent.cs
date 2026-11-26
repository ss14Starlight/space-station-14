using System.Linq;
using Robust.Shared.GameStates;

namespace Content.Server._Starlight.NPC.Components;

/// <summary>
/// Lets an entity without a controlling player periodically say a line from a dialogue pool.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class NPCSpeechComponent : Component
{
    private const string SpeechSeparator = " | ";
    private const string LocalizationPrefix = "loc:";
    private const string LiteralPrefix = "literal:";

    private TimeSpan _minimumInterval = TimeSpan.FromSeconds(20);
    private TimeSpan _maximumInterval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Minimum interval between attempts in seconds. The effective minimum is at least 0.5 seconds.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan MinimumInterval
    {
        get => _minimumInterval;
        set
        {
            if (_minimumInterval == value)
                return;

            _minimumInterval = value;
            AttemptInterval = null;
        }
    }

    /// <summary>
    /// Maximum interval between attempts in seconds. Values below the effective minimum are raised to that minimum.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan MaximumInterval
    {
        get => _maximumInterval;
        set
        {
            if (_maximumInterval == value)
                return;

            _maximumInterval = value;
            AttemptInterval = null;
        }
    }

    /// <summary>
    /// Probability from zero to one of choosing an entry on each attempt. Zero disables attempts.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SpeechChance = 0.35f;

    /// <summary>
    /// Dialogue entries may be Fluent IDs or legacy literal text. Use <c>loc:</c> for an explicit ID
    /// and <c>literal:</c> for text that resembles an ID. Prefix the resulting text with an asterisk to emote.
    /// </summary>
    [DataField("speech"), ViewVariables(VVAccess.ReadOnly)]
    internal List<string> SpeechPool = ["npc-speech-default"];

    /// <summary>
    /// Optional literal dialogue override. An empty list disables dialogue and null uses <see cref="SpeechPool"/>.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    internal List<string>? LiteralSpeech;

    /// <summary>
    /// Displays the active dialogue pool in View Variables. Edits replace it with literal lines separated by <c> | </c>.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public string Speech
    {
        get
        {
            if (LiteralSpeech is { } literal)
                return string.Join(SpeechSeparator, literal);

            var loc = IoCManager.Resolve<ILocalizationManager>();
            return string.Join(SpeechSeparator, SpeechPool.Select(line => ResolveLine(loc, line)).OfType<string>());
        }
        set => LiteralSpeech = new List<string>(value.Split(SpeechSeparator,
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }

    internal static string? ResolveLine(ILocalizationManager loc, string line)
    {
        line = line.Trim();
        if (line.Length == 0)
            return line;

        if (line.StartsWith(LiteralPrefix, StringComparison.Ordinal))
            return line[LiteralPrefix.Length..].TrimStart();

        if (line.StartsWith(LocalizationPrefix, StringComparison.Ordinal))
        {
            var key = line[LocalizationPrefix.Length..].Trim();
            return key.Length != 0 && loc.TryGetString(key, out var explicitText) ? explicitText : null;
        }

        if (loc.TryGetString(line, out var localized))
            return localized;

        // Reserve bare kebab-case IDs while allowing older prose, one-word dialogue, and emotes.
        var looksLikeKey = char.IsAsciiLetterLower(line[0]) && line.Contains('-') && !line.EndsWith('.') &&
            line.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character is '-' or '_' or '.');
        return looksLikeKey ? null : line;
    }

    [AutoPausedField]
    internal TimeSpan? LastAttemptTime;

    internal TimeSpan? AttemptInterval;
}
