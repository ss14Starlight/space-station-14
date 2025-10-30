using Robust.Shared.Serialization;

namespace Content.Shared.Administration
{

    [Serializable, NetSerializable]
    public sealed class AutoModRule
    {
        public int Id { get; set; }
        public string Regex = string.Empty;
        public bool Enabled { get; set; }
        public List<AutoModOffence> Offences { get; set; } = new();
    }

    [Serializable, NetSerializable]
    public sealed class AutoModOffence
    {
        public string Message = string.Empty;
        public AutoModOffenceAction Action = AutoModOffenceAction.Clear;
        /// <summary>
        /// Ban duration in minutes. 0 = permanent ban.
        /// </summary>
        public int BanDurationMinutes = 0;
        /// <summary>
        /// How long (in seconds) to wait before decaying to the previous offence level. 0 = never decay.
        /// </summary>
        public int DecaySeconds = 0;
        public bool CancelSpeech = false;
    }

    [Serializable, NetSerializable]
    public enum AutoModOffenceAction
    {
        Clear = 0,
        Warn = 1,
        Kick = 2,
        Ban = 3
    }
}
