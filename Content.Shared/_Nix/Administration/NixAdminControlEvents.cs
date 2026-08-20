using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;

namespace Content.Shared._Nix.Administration;

public enum NixAdminAudioAction : byte
{
    Play,
    StopAll,
    PrepareUpload
}

public enum NixAdminAudioResult : byte
{
    Playing,
    Stopped,
    UploadReady,
    Prepared,
    InvalidPath,
    UploadRejected
}

public enum NixAdminPowerAction : byte
{
    RechargeSmes,
    RestoreStationApcs
}

[Serializable, NetSerializable]
public sealed class NixAdminAudioCommandEvent : EntityEventArgs
{
    public NixAdminAudioAction Action;
    public string Path = string.Empty;
    public byte VolumePercent = 100;
}

[Serializable, NetSerializable]
public sealed class NixAdminAudioResultEvent : EntityEventArgs
{
    public NixAdminAudioResult Result;
    public string Path = string.Empty;
    public byte VolumePercent;
}

[Serializable, NetSerializable]
public sealed class NixStopAdminAudioEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class NixAdminPowerCommandEvent : EntityEventArgs
{
    public NixAdminPowerAction Action;
}

[Serializable, NetSerializable]
public sealed class NixAdminPowerResultEvent : EntityEventArgs
{
    public NixAdminPowerAction Action;
    public int AffectedCount;
}

[Serializable, NetSerializable]
public sealed class NixAdminAlertRequestEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class NixAdminSetAlertEvent : EntityEventArgs
{
    public NetEntity Station;
    public string Level = string.Empty;
    public bool Locked;
}

[Serializable, NetSerializable]
public sealed class NixAdminAlertLevelData
{
    public string Id = string.Empty;
    public bool Selectable;
}

[Serializable, NetSerializable]
public sealed class NixAdminStationAlertData
{
    public NetEntity Station;
    public string Name = string.Empty;
    public string CurrentLevel = string.Empty;
    public bool Locked;
    public List<NixAdminAlertLevelData> Levels = new();
}

[Serializable, NetSerializable]
public sealed class NixAdminAlertSnapshotEvent : EntityEventArgs
{
    public List<NixAdminStationAlertData> Stations = new();
}

[Serializable, NetSerializable]
public sealed class NixAdminAlertResultEvent : EntityEventArgs
{
    public bool Success;
    public NetEntity Station;
    public string Level = string.Empty;
    public bool Locked;
}

[Serializable, NetSerializable]
public sealed class NixAdminSecureActionEvent : EntityEventArgs
{
    public string RequestId = string.Empty;
}

[Serializable, NetSerializable]
public sealed class NixAdminSecureActionResultEvent : EntityEventArgs
{
    public bool Success;
    public string RequestId = string.Empty;
}

public enum NixAdminShuttleAction : byte
{
    Call,
    Recall,
    SelectEmergencyShuttle,
    SetPurchaseLock
}

[Serializable, NetSerializable]
public sealed class NixAdminShuttleRequestEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class NixAdminShuttleCommandEvent : EntityEventArgs
{
    public NixAdminShuttleAction Action;
    public string ShuttlePath = string.Empty;
    public float Seconds;
    public bool Locked;
}

[Serializable, NetSerializable]
public sealed class NixAdminShuttleSnapshotEvent : EntityEventArgs
{
    public bool Called;
    public bool SelectionLocked;
    public float SecondsRemaining;
    public bool PurchasesLocked;
    public string CurrentShuttlePath = string.Empty;
    public List<string> AvailableShuttles = new();
}

[Serializable, NetSerializable]
public sealed class NixAdminShuttleResultEvent : EntityEventArgs
{
    public bool Success;
    public NixAdminShuttleAction Action;
}
