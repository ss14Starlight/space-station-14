using Content.Shared.Armor;
using Content.Shared._Starlight.Supermatter.Components;

namespace Content.Shared._Starlight.Supermatter.EntitySystems;

/// <summary>
/// Handles armor examine for supermatter immunity.
/// </summary>
public sealed class SupermatterImmuneSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SupermatterImmuneComponent, ArmorExamineEvent>(OnArmorExamine);
    }

    private void OnArmorExamine(Entity<SupermatterImmuneComponent> ent, ref ArmorExamineEvent args)
    {
        args.Msg.PushNewline();
        args.Msg.AddMarkupOrThrow(Loc.GetString("supermatter-immune-examine"));
    }
}
