using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.NPC;

[RegisterComponent, NetworkedComponent, Access(typeof(HeldFactionSystem))]
public sealed partial class HeldFactionComponent : Component
{
    [DataField(required: true)]
    public string Faction = string.Empty;

    [DataField]
    public bool AlreadyMember;
}
