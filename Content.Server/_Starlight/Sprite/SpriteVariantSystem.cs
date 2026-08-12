using Content.Shared._Starlight.Roles;
using Content.Shared._Starlight.Sprite;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Sprite;

public sealed partial class SpriteVariantSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpriteVariantComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SpriteVariantComponent, RoleLoadoutAppliedEvent>(OnRoleLoadoutApplied);
    }

    private void OnMapInit(EntityUid uid, SpriteVariantComponent comp, MapInitEvent ev)
    {
        if (!string.IsNullOrEmpty(comp.Variant) || comp.AvailableVariants.Count == 0)
            return;

        comp.Variant = _random.Pick(comp.AvailableVariants);
        Dirty(uid, comp);
    }

    /// <summary>
    /// Applies a player-picked variant from their role loadout, if one was
    /// selected. Runs after MapInit, so this overrides any random pick that
    /// already happened rather than deferring to it.
    /// </summary>
    private void OnRoleLoadoutApplied(EntityUid uid, SpriteVariantComponent comp, RoleLoadoutAppliedEvent ev)
    {
        foreach (var selections in ev.Loadout.SelectedLoadouts.Values)
        {
            foreach (var selection in selections)
            {
                if (!comp.AvailableVariants.Contains(selection.Prototype))
                    continue;

                comp.Variant = selection.Prototype;
                Dirty(uid, comp);
                return;
            }
        }
    }
}
