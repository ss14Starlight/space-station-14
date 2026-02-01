namespace Content.Shared._Starlight.Radio;

[Serializable]
[DataDefinition]
public sealed partial class CustomRadioChannelData
{
    [DataField] public string Id { get; set; } = string.Empty;
    [DataField] public LocId Name { get; set; } = string.Empty;
    [ViewVariables(VVAccess.ReadOnly)] public string LocalizedName => Loc.GetString(Name);
    [DataField] public char Keycode { get; set; }
    [DataField] public int Frequency { get; set; }
    [DataField] public Color Color { get; set; } = Color.Lime;
    [DataField] public bool LongRange { get; set; }
}

public interface ISupportsCustomChannels
{
    public HashSet<CustomRadioChannelData> CustomChannels { get; set; }
}