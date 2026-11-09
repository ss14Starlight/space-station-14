using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.FireDragon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PredatorHuntStatusEffectComponent : Component
{
    /// <summary>
    /// Color and opacity of each Predator Hunt screen-border band.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color OverlayColor = new(0.9f, 0.15f, 0.02f, 0.035f);

    /// <summary>
    /// Number of overlapping bands used to draw the screen border.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int OverlayBands = 6;

    /// <summary>
    /// Width of each border band as a fraction of the viewport height.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float OverlayBandThickness = 0.008f;
}
