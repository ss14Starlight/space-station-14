using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Access.Systems;

public record GetAccessReaderDenyTagsEvent(
    HashSet<ProtoId<AccessLevelPrototype>> DenyTags);

public record GetAccessReaderAccessListsEvent(
    List<HashSet<ProtoId<AccessLevelPrototype>>> AccessLists);
