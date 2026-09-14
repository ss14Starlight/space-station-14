using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.WallStains.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class FlammableWallStainComponent : Component
{
    [ViewVariables]
    public bool OnFire { get; set; }

    [ViewVariables]
    public int Flammability { get; set; }

    [ViewVariables]
    public float Accumulator { get; set; }

    [ViewVariables]
    public EntityUid? PlayingStream { get; set; }

    [ViewVariables]
    public string? CurrentPlayingSound { get; set; }

    [ViewVariables]
    public EntityUid? FireEffectEntity { get; set; }

    [ViewVariables]
    public int FireState { get; set; } = 4;
}
