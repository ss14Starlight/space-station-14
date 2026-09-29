using System.Numerics;

namespace Content.Client._Starlight.Actions.Components;

/// <summary>
/// The K9's true resting sprite offset for the bite-shake animation, so
/// restarts mid-wiggle don't compound a drift.
/// </summary>
[RegisterComponent]
public sealed partial class LatchBiteShakeVisualsComponent : Component
{
    public Vector2 BaseOffset;
}
