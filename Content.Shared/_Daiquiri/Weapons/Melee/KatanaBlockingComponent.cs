using Robust.Shared.Timing;

namespace Content.Shared._Daiquiri.Weapons.Melee;

[RegisterComponent]
public sealed partial class KatanaBlockingComponent : Component
{
    public EntityUid? Katana;

    public EntityUid? PendingAttacker;

    public GameTick PendingTick;
}
