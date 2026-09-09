using Robust.Shared.Utility;

namespace Content.Shared._Funkystation.Footprints;

public sealed partial class FootprintComponent : Component
{
    [DataField]
    public ResPath Sprites = new("/Textures/_Funkystation/Effects/footprints.rsi");
}
