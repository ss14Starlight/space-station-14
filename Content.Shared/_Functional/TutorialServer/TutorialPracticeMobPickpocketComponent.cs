using Robust.Shared.GameStates;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Marks a practice victim whose jumpsuit/pocket loot is filled after tutorial spawn.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialPracticeMobPickpocketComponent : Component;
