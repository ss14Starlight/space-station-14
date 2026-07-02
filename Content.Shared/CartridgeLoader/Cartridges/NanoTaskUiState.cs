using Robust.Shared.Serialization;

namespace Content.Shared.CartridgeLoader.Cartridges;

/// <summary>
///     The priority assigned to a NanoTask item
/// </summary>
[Serializable, NetSerializable]
public enum NanoTaskPriority : byte
{
    High,
    Medium,
    Low,
};

/// <summary>
///     The data relating to a single NanoTask item, but not its identifier
/// </summary>
[Serializable, NetSerializable, DataRecord]
public sealed partial class NanoTaskItem
{
    /// <summary>
    ///     The maximum length of the text fields
    /// </summary>
    public static int MaximumStringLength = 30;

    /// <summary>
    ///     The task description, i.e. "Bake a cake"
    /// </summary>
    public readonly string Description;

    /// <summary>
    ///     Who the task is for. Starlight - Tidr: stamped server-side from the poster's ID card.
    /// </summary>
    public readonly string TaskIsFor;

    /// <summary>
    ///     If the task is marked as done or not
    /// </summary>
    public readonly bool IsTaskDone;

    /// <summary>
    ///     The task's marked priority
    /// </summary>
    public readonly NanoTaskPriority Priority;

    /// <summary>
    ///     Starlight - Tidr: where the requester can be met to hand off / collect.
    /// </summary>
    public readonly string Location;

    /// <summary>
    ///     Starlight - Tidr: credit reward offered for completing the task. Held in escrow while the task is open.
    /// </summary>
    public readonly int Reward;

    /// <summary>
    ///     Starlight - Tidr: name on the ID card of the Tider who accepted the job, or null if unclaimed.
    /// </summary>
    public readonly string? AcceptedBy;

    public NanoTaskItem(string description, string taskIsFor, bool isTaskDone, NanoTaskPriority priority, string location = "", int reward = 0, string? acceptedBy = null)
    {
        Description = description;
        TaskIsFor = taskIsFor;
        IsTaskDone = isTaskDone;
        Priority = priority;
        Location = location;
        Reward = reward;
        AcceptedBy = acceptedBy;
    }
    public bool Validate()
    {
        return Description.Length <= MaximumStringLength
            && TaskIsFor.Length <= MaximumStringLength
            && Location.Length <= MaximumStringLength
            && Reward >= 0;
    }
};

/// <summary>
///     Pairs a NanoTask item and its identifier
/// </summary>
[Serializable, NetSerializable, DataRecord]
public sealed partial class NanoTaskItemAndId
{
    public readonly int Id;
    public readonly NanoTaskItem Data;

    public NanoTaskItemAndId(int id, NanoTaskItem data)
    {
        Id = id;
        Data = data;
    }
};

/// <summary>
///     Starlight - Tidr: a task plus flags describing the viewing PDA's relationship to it.
///     Computed server-side per PDA from the inserted ID card, so the client can show
///     the edit form vs a read-only details card, and the accept vs release vs locked button.
/// </summary>
[Serializable, NetSerializable, DataRecord]
public sealed partial class NanoTaskViewerEntry
{
    public readonly NanoTaskItemAndId Task;
    public readonly bool ViewerIsOwner;
    public readonly bool ViewerIsAccepter;

    public NanoTaskViewerEntry(NanoTaskItemAndId task, bool viewerIsOwner, bool viewerIsAccepter)
    {
        Task = task;
        ViewerIsOwner = viewerIsOwner;
        ViewerIsAccepter = viewerIsAccepter;
    }
};

/// <summary>
///     The UI state of the NanoTask/Tidr app
/// </summary>
[Serializable, NetSerializable]
public sealed class NanoTaskUiState : BoundUserInterfaceState
{
    public List<NanoTaskViewerEntry> Tasks;

    /// <summary>
    ///     Starlight - Tidr: the viewing player's credit balance, shown in the app header.
    ///     -1 means "couldn't resolve an account" and the client hides the readout.
    /// </summary>
    public int ViewerBalance;

    public NanoTaskUiState(List<NanoTaskViewerEntry> tasks, int viewerBalance = -1)
    {
        Tasks = tasks;
        ViewerBalance = viewerBalance;
    }
}
