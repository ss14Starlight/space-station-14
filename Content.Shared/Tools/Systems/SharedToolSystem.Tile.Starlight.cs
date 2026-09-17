using Content.Shared.SubFloor;

namespace Content.Shared.Tools.Systems;

public abstract partial class SharedToolSystem
{
    /// <summary>
    /// Whether an entity is a subfloor device that a floor tile is currently covering. Such entities neither block
    /// modification of the tile above them nor stop a tile tool from targeting it.
    /// </summary>
    private bool IsSubfloorCovered(Entity<SubFloorHideComponent?> ent)
        => Resolve(ent, ref ent.Comp, false) && ent.Comp.IsUnderCover;
}
