using Content.Shared._Starlight.Roles;
using Content.Shared._Starlight.Sprite;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Sprite;

public sealed partial class SpriteVariantSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpriteVariantComponent, ComponentStartup>(OnVariantStartup);
        SubscribeLocalEvent<SpriteVariantComponent, AfterAutoHandleStateEvent>(OnVariantState);
        SubscribeLocalEvent<SpriteVariantComponent, RoleLoadoutAppliedEvent>(OnRoleLoadoutApplied);
    }

    private void OnVariantStartup(EntityUid uid, SpriteVariantComponent comp, ComponentStartup ev)
    {
        TryApplyVariant(uid, comp);
    }

    private void OnVariantState(EntityUid uid, SpriteVariantComponent comp, ref AfterAutoHandleStateEvent ev)
    {
        TryApplyVariant(uid, comp);
    }

    /// <summary>
    /// Handles the character-preview dummy, which is spawned client-side only
    /// and never reaches the server's copy of this same handler.
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
                TryApplyVariant(uid, comp);
                return;
            }
        }
    }

    private void TryApplyVariant(EntityUid uid, SpriteVariantComponent variant)
    {
        if (string.IsNullOrEmpty(variant.Variant))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.LayerSetRsiState((uid, sprite), SpriteVariantLayers.Base, variant.Variant);
    }
}

public enum SpriteVariantLayers : byte
{
    Base,
}
