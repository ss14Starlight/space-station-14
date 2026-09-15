namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Marks practice-spawned entities so sensors can listen for UseInHand without global subscriptions.
/// </summary>
[RegisterComponent]
public sealed partial class TutorialSensorTargetComponent : Component;
