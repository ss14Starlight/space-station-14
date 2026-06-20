namespace Content.Server.Medical.CrewMonitoring;

/// <summary>
///     STARLIGHT: Records the ID of whoever gets implanted by a command tracking implant.
///     used by the BSO's Crew Monitor as a QOL thing so that people dont go from HoP to unknown,
///     or just go to unknown and sit at the bottom of the crew monitor
/// </summary>
[RegisterComponent]
public sealed partial class StoredImplantIdentityComponent : Component
{
    /// <summary>Whether an identity has been captured yet.</summary>
    [DataField]
    public bool Captured;

    [DataField]
    public string? Name;

    [DataField]
    public string? Job;

    [DataField]
    public string? JobIcon;

    [DataField]
    public List<string> JobDepartments = new();
}
