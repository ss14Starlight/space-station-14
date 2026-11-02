using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.StationRadio;

[Serializable, NetSerializable]
public enum VinylPlayerVisuals : byte
{
    Disc,

    LabelColor,

    SlotMask,
}

[Serializable, NetSerializable]
public enum VinylPlayerVisualLayers : byte
{
    Disc,
    Label,

    Display,
}
