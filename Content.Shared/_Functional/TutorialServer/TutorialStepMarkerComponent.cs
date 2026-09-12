using Robust.Shared.GameStates;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Marks a practice-map entity as a reach target for <see cref="TutorialStepComplete.ReachMarker"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialStepMarkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public string MarkerId = string.Empty;
}
