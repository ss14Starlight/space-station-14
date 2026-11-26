using Robust.Shared.GameStates;

namespace Content.Server._Starlight.NPC.Components;

/// <summary>
/// Lets an entity without a controlling player periodically say a line from a dialogue pool.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class NPCSpeechComponent : Component
{
    private const string SpeechSeparator = " | ";

    private TimeSpan _minimumInterval = TimeSpan.FromSeconds(20);
    private TimeSpan _maximumInterval = TimeSpan.FromSeconds(60);

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

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SpeechChance = 0.35f;

    [DataField("speech"), ViewVariables(VVAccess.ReadOnly)]
    internal List<string> SpeechPool = ["Hello world!"]; //Placeholder

    /// <summary>
    /// Single-line text editor for the dialogue pool in View Variables.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public string Speech
    {
        get => string.Join(SpeechSeparator, SpeechPool);
        set => SpeechPool = new List<string>(value.Split(SpeechSeparator,
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }

    [AutoPausedField]
    internal TimeSpan? LastAttemptTime;

    internal TimeSpan? AttemptInterval;
}
