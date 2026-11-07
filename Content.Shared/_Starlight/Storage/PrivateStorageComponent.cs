namespace Content.Shared._Starlight.Storage;

/// <summary>
/// Component that defines various variables used in our StorageActionSystem, namely delay of opening, and popup texts
/// </summary>
[RegisterComponent, Access(typeof(SharedPrivateStorageSystem))]
public sealed partial class PrivateStorageComponent : Component
{
    public static readonly TimeSpan DefaultAccessDelay = TimeSpan.FromSeconds(7);
    public const string DefaultAccessPopup = "action-storage-accessing-outsider";

    /// <summary>
    /// The amount of time it takes to open the storage for an outsider
    /// </summary>
    [DataField("accessDelay")] public TimeSpan AccessDelay = DefaultAccessDelay;

    /// <summary>
    /// A popup that will be shown when storage is accesses by an outsider
    /// </summary>
    [DataField("accessPopup")] public LocId AccessPopup = DefaultAccessPopup;
}
