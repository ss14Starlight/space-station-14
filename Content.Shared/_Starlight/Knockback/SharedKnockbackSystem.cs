using System.Numerics;
using Content.Shared.Clothing;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Slippery;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Map;

//linq
using Content.Shared.Examine;

namespace Content.Shared._Starlight.Knockback;

public abstract partial class SharedKnockbackSystem : EntitySystem
{
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] protected SharedTransformSystem _transformSystem = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<KnockbackByUserTagComponent, OnNonEmptyGunShotEvent>(OnGunShot);
        SubscribeLocalEvent<KnockbackByUserTagComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<KnockbackByUserTagComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
        {
            //check if the examiner has any tags that match the component's tags
            if (GetKnockbackData(ent, args.Examiner, out KnockbackData data))
            {
                // Magboot check.
                var hasMagBootsEnabled = false;
                if (_inventory.TryGetSlotEntity(args.Examiner, "shoes", out var boots) &&
                    TryComp<MagbootsComponent>(boots.Value, out _) &&
                    _toggle.IsActivated(boots.Value))
                {
                    hasMagBootsEnabled = true;
                }
                var knockback = CalculateKnockback(args.Examiner, data, hasMagBootsEnabled);
                //figure out the forwards/backwards direction
                var direction = knockback < 0 ? "forwards" : "backwards";
                // args to push to examine text.
                args.PushMarkup(Loc.GetString("knockback-by-user-tag-component-examine-distance", ("knockback", String.Format("{0:0.###}", MathF.Abs(knockback))), ("direction", direction)));
                args.PushMarkup(Loc.GetString("knockback-by-user-tag-component-examine-stamina", ("stamina", String.Format("{0:0.###}", CalculateStaminaDamage(data,hasMagBootsEnabled)))));
            }
        }
    }

    private void OnGunShot(Entity<KnockbackByUserTagComponent> ent, ref OnNonEmptyGunShotEvent args)
    {
        //make sure the ammo is shootable
        foreach (var ammo in args.Ammo)
        {
            if (TryComp<CartridgeAmmoComponent>(ammo.Uid, out var cartridge))
            {
                //check if its spent
                if (cartridge.Spent)
                {
                    return;
                }
            }
        }

        EntityUid user = args.User;

        //check for tags
        if (GetKnockbackData(ent, user, out var data))
        {
            var hasMagBootsEnabled = false;
            // Check for magboots.
            if (_inventory.TryGetSlotEntity(user, "shoes", out var boots) &&
                TryComp<MagbootsComponent>(boots.Value, out _) &&
                _toggle.IsActivated(boots.Value))
            {
                hasMagBootsEnabled = true;
            }
            // Early return.
            if(data.IsDisabledByMagboots && hasMagBootsEnabled)
                return;
            //get the gun component
            if (TryComp<GunComponent>(ent, out var gunComponent))
            {
                var toCoordinates = gunComponent.ShootCoordinates;

                if (toCoordinates == null)
                    return;

                var knockback = CalculateKnockback(user, data);

                if (knockback == 0.0f)
                    return;

                //make a clone, not a reference
                Vector2 modifiedCoords = toCoordinates.Value.Position;
                //flip the direction
                if (knockback > 0)
                    modifiedCoords = -modifiedCoords;

                //absolute knockback now
                knockback = Math.Abs(knockback);
                //normalize them
                modifiedCoords = Vector2.Normalize(modifiedCoords);
                //multiply by the knockback value
                modifiedCoords *= knockback;
                //set the new coordinates
                var flippedDirection = new EntityCoordinates(user, modifiedCoords);

                if (data.IsReducedByMagboots && hasMagBootsEnabled)
                {
                    _throwing.TryThrow(user, flippedDirection, knockback * 5, user, 0, doSpin: false, compensateFriction: true, doFly: false);
                }
                else
                {
                    _throwing.TryThrow(user, flippedDirection, knockback * 5, user, 0, doSpin: false, compensateFriction: true, doFly: data.DoFly);
                }

                //deal stamina damage
                if (TryComp<StaminaComponent>(user, out var stamina))
                {
                    _stamina.TakeStaminaDamage(user, CalculateStaminaDamage(data, hasMagBootsEnabled), component: stamina);
                }
            }
        }
    }

    private bool GetKnockbackData(Entity<KnockbackByUserTagComponent> ent, EntityUid user, out KnockbackData data)
    {
        KnockbackData totalData = new();
        bool hadAnyMatches = false;
        //get all matching tags
        foreach (var tag in ent.Comp.DoestContain.Keys)
        {
            if (_tagSystem.HasTag(user, tag))
            {
                var tagdata = ent.Comp.DoestContain[tag];
                totalData.Knockback += tagdata.Knockback;
                totalData.StaminaDamage += tagdata.StaminaDamage;
                totalData.DoFly = tagdata.DoFly;
                totalData.IsDisabledByMagboots = tagdata.IsDisabledByMagboots;
                totalData.IsReducedByMagboots = tagdata.IsReducedByMagboots;
                totalData.MagbootReductionMultiplier = tagdata.MagbootReductionMultiplier;
                hadAnyMatches = true;
            }
        }

        data = totalData;

        return hadAnyMatches;
    }

    private float CalculateKnockback(EntityUid user, KnockbackData data, bool magBoots = false)
    {
        if(data.IsDisabledByMagboots && magBoots)
            return 0;
        float knockback = data.Knockback;
        //If we have no slips, cut the knockback in half
        if (CheckForNoSlips(user))
        {
            knockback *= 0.5f;
        }
        else if (data.IsReducedByMagboots && magBoots)
        {
            knockback *= 1f - (data.MagbootReductionMultiplier / 100f);
        }

        return knockback;
    }

    private static float CalculateStaminaDamage(KnockbackData data, bool magBoots = false)
    {
        if(data.IsDisabledByMagboots && magBoots)
            return 0;
        else if(data.IsReducedByMagboots && magBoots)
            return data.StaminaDamage * (1f - (data.MagbootReductionMultiplier / 100f));
        else
            return data.StaminaDamage;
    }

    private bool CheckForNoSlips(EntityUid uid)
    {
        if (EntityManager.TryGetComponent(uid, out NoSlipComponent? flashImmunityComponent))
        {
            return true;
        }

        if (TryComp<InventoryComponent>(uid, out var inventoryComp))
        {
            //get all worn items
            var slots = _inventory.GetSlotEnumerator((uid, inventoryComp), SlotFlags.WITHOUT_POCKET);
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity != null && EntityManager.TryGetComponent(slot.ContainedEntity, out NoSlipComponent? wornNoSlipComponent))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
