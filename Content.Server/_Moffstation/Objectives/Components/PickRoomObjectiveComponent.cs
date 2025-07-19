using Content.Shared.Pinpointer;
using Robust.Shared.Prototypes;

namespace Content.Server._Moffstation.Objectives.Components;

[RegisterComponent]
public sealed partial class PickRoomObjectiveComponent : Component
{
    [DataField]
    public HashSet<string> RoomBlacklist = new();

    [DataField]
    public HashSet<string> RoomWhitelist = new();
}
