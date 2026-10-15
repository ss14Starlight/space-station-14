using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.FireDragon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PredatorHuntStatusEffectComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color OverlayColor = new(0.9f, 0.15f, 0.02f, 0.035f);

    [DataField, AutoNetworkedField]
    public int OverlayBands = 6;

    /// <summary>
    /// Width of each border band as a fraction of the viewport height.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float OverlayBandThickness = 0.008f;
}
