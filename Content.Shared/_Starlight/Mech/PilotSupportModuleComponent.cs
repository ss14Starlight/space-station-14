using Content.Shared.Whitelist;

namespace Content.Shared._Starlight.Mech;

[RegisterComponent]
public sealed partial class PilotSupportModuleComponent : Component
{
    [DataField]
    public EntityWhitelist? PilotWhitelist;
}