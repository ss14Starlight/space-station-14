namespace Content.Shared._Starlight.CustomSpawner;

[RegisterComponent]
public sealed partial class CustomSpawnerHologramComponent : Component
{
    [DataField] public string Rsi = string.Empty;
    [DataField] public string State = string.Empty;
    [DataField] public string ShaderName = "Hologram";
    [DataField] public Color Color1 = Color.White;
    [DataField] public Color Color2 = Color.White;
    [DataField] public float Alpha = 1;
    [DataField] public float Intensity = 2;
    [DataField] public float ScrollRate = 0.125f;
}
