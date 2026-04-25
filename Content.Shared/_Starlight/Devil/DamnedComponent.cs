using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Devil;

[RegisterComponent, NetworkedComponent]
public sealed partial class DamnedComponent : Component
{
    [DataField]
    public SoundSpecifier DamnedPunishmentSound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");

    public List<ProtoId<DamnationPrototype>> Damnations = new();

    public int NetCost = 0;

    public EntityUid DamnedBy;
}
