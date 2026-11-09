using Content.Shared.Hands;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;

namespace Content.Shared._Starlight.NPC;

public sealed class HeldFactionSystem : EntitySystem
{
    [Dependency] private NpcFactionSystem _faction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeldFactionComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<HeldFactionComponent, GotUnequippedHandEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<HeldFactionComponent> ent, ref GotEquippedHandEvent args)
    {
        TryComp<NpcFactionMemberComponent>(args.User, out var factionComp);
        var faction = (args.User, factionComp);
        ent.Comp.AlreadyMember = _faction.IsMember(faction, ent.Comp.Faction);
        _faction.AddFaction(faction, ent.Comp.Faction);
    }

    private void OnUnequipped(Entity<HeldFactionComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (ent.Comp.AlreadyMember)
        {
            ent.Comp.AlreadyMember = false;
            return;
        }

        _faction.RemoveFaction(args.User, ent.Comp.Faction);
    }
}
