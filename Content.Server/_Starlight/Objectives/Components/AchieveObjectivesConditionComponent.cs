namespace Content.Server._Starlight.Objectives.Components;

/// <summary>
/// Completes when the mind's other objectives with the same ObjectiveComponent.Antag are complete.
/// Other achieve-objectives conditions are excluded to avoid circular progress checks.
/// </summary>
[RegisterComponent]
public sealed partial class AchieveObjectivesConditionComponent : Component;
