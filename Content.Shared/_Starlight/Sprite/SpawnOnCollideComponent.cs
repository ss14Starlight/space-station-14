using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Sprite;
[RegisterComponent, NetworkedComponent]
public sealed partial class SpriteWhitelistedComponent : Component
{
    [DataField]
    public EntityWhitelist? LocalEntityWhitelist;

    [DataField]
    public Dictionary<string, PrototypeLayerData> PassedLayers = [];

    [DataField]
    public Dictionary<string, PrototypeLayerData> FailedLayers = [];

}
