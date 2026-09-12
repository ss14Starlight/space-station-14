using Content.Shared._Functional.TutorialServer;
using Robust.Shared.GameStates;

namespace Content.Shared._Functional.TutorialServer.StarlightSurgery;

/// <summary>
/// Enables the Starlight-style surgery Bound UI on this entity (tutorial NPCs only).
/// Tracks incision / implant progress without Starlight's body-part organ slots.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialStarlightSurgeryTargetComponent : Component
{
    /// <summary>
    /// Virtual parts the BUI lists (Starlight enumerates real body parts; we keep a fixed set).
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> Parts = new List<string> { "Head" };

    /// <summary>Completed step keys as "SurgeryId:StepId".</summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> CompletedSteps = new();

    [DataField, AutoNetworkedField]
    public HashSet<string> CompletedSurgeries = new();

    [DataField, AutoNetworkedField]
    public HashSet<string> StartedSurgeries = new();

    /// <summary>True once an eye cybernetic has been surgically inserted.</summary>
    [DataField, AutoNetworkedField]
    public bool HasEyeImplant;

    /// <summary>
    /// True after the example implant path is finished (implant inserted and incision closed).
    /// Used by the tutorial goal sensor.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ExampleSurgeryComplete;

    /// <summary>
    /// Only surgeons currently in this tutorial role may open / use this Bound UI.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string RequiredRoleId = TutorialSurgeryRoleLock.StarlightRoleId;
}
