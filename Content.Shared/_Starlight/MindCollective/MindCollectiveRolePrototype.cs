using Content.Shared._Starlight.BodyControl;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.MindCollective;

/// <summary>Permissions are extensible identifiers interpreted by the systems performing the operation.</summary>
[Prototype]
public sealed partial class MindCollectiveRolePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public LocId Name = string.Empty;
    [DataField] public int Priority;
    [DataField] public bool Unique;
    [DataField] public HashSet<string> Permissions = new();
    [DataField] public BodyControlAccess Control = new();
}
