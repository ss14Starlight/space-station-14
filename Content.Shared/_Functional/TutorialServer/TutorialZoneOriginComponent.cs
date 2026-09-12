namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Marks the practice-spawn origin (chamber center) on a tutorial room template.
/// When absent, the stamp system uses the template tile AABB center.
/// </summary>
[RegisterComponent]
public sealed partial class TutorialZoneOriginComponent : Component;
