namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Marker stamped on some tutorial grids. Prefer
/// <see cref="TutorialRolePrototype.SimplifiedEnvironment"/> for force-power (atmos freeze TEMPORARILY off);
/// this component alone no longer drives load-time power stubbing.
/// </summary>
[RegisterComponent]
public sealed partial class TutorialForcePowerGridComponent : Component;
