using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;

// Starlight AutoMod shared data
namespace Content.Shared._Starlight.Administration;

[Serializable, NetSerializable]
public sealed class AutoModRule
{
    public int Id { get; set; }
    public string? Category { get; set; }
    public int Severity { get; set; } = 1;
    public string Regex { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    
    /// <summary>
    /// If true, this rule will also watch OOC channels (LOOC, OOC, Dead/Ghost chat). Default is false.
    /// </summary>
    public bool WatchOOC { get; set; } = false;
    
    public List<AutoModOffence> Offences { get; set; } = new();
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid LastModifiedBy { get; set; }
    public DateTime LastModifiedAt { get; set; }
}

[Serializable, NetSerializable]
public sealed class AutoModOffence
{
    public string Message { get; set; } = string.Empty;
    public AutoModOffenceAction Action { get; set; } = AutoModOffenceAction.None;
    
    public int BanDurationMinutes { get; set; } = 0;
    public int DecaySeconds { get; set; } = 0;
    public int DecayLevels { get; set; } = 1;
    public bool Persistent { get; set; } = true;
    public bool CancelSpeech { get; set; } = false;
}

[Serializable, NetSerializable]
public enum AutoModSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

[Serializable, NetSerializable]
public enum AutoModOffenceAction
{
    None = 0,
    Warn = 1,
    Kick = 2,
    Ban = 3
}
