using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Starlight.CryoTeleportation;

[RegisterComponent]
public sealed partial class StationCryoTeleportationComponent : Component
{
    [DataField]
    public TimeSpan TransferDelay = TimeSpan.FromSeconds(420); //7 Minutes

    [DataField]
    public EntProtoId PortalPrototype = "CryoPortal";

    [DataField]
    public SoundSpecifier TransferSound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
}
