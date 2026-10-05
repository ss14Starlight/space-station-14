using Robust.Shared.Prototypes;

namespace Content.Shared.Access.Systems;

public record GetAccessReaderDenyTagsEvent(
    HashSet<ProtoId<AccessLevelPrototype>> DenyTags);

public record GetAccessReaderAccessListsEvent(
    List<HashSet<ProtoId<AccessLevelPrototype>>> AccessLists);


