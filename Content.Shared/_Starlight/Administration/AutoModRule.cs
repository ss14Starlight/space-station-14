using Robust.Shared.Serialization;

namespace Content.Shared.Administration
{

    [Serializable, NetSerializable]
    public sealed class AutoModRule
    {
        public int Id { get; set; }
        public string Regex = string.Empty;
        public AutoModSeverity Severity { get; set; }
        // Deprecated: public string Message = string.Empty;
        public int Count { get; set; }
        public bool Enabled { get; set; }
        public bool CancelSpeech { get; set; }

        // New: List of offences for this rule
        public List<AutoModOffence> Offences { get; set; } = new();
    }

    [Serializable, NetSerializable]
    public sealed class AutoModOffence
    {
    public string Message = string.Empty;
    public AutoModOffenceAction Action = AutoModOffenceAction.Clear;
    /// <summary>
    /// Ban duration in seconds. 0 = permanent ban.
    /// </summary>
    public int BanDurationSeconds = 0;
    /// <summary>
    /// How long (in seconds) to wait before decaying to the previous offence level. 0 = never decay.
    /// </summary>
    public int DecaySeconds = 0;
    }

    [Serializable, NetSerializable]
    public enum AutoModOffenceAction
    {
        Clear = 0,
        Warn = 1,
        Kick = 2,
        Ban = 3
    }

    public enum AutoModSeverity
    {
        None = 0,
        Warning = 1,
        Kick = 2,
        Ban = 3,
    }
}
