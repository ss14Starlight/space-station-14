using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

#region Starlight
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
#endregion

namespace Content.Shared.Blocking;

public sealed partial class BlockingSystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private PowerCellSystem _powerCell = default!; //Starlight
    [Dependency] private ItemToggleSystem _itemToggle = default!; //Starlight

    private void InitializeUser()
    {
        SubscribeLocalEvent<BlockingUserComponent, DamageModifyEvent>(OnUserDamageModified);
        SubscribeLocalEvent<BlockingComponent, DamageModifyEvent>(OnDamageModified);

        SubscribeLocalEvent<BlockingUserComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<BlockingUserComponent, ContainerGettingInsertedAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<BlockingUserComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<BlockingUserComponent, EntityTerminatingEvent>(OnEntityTerminating);

        SubscribeLocalEvent<BlockingComponent, ItemToggledEvent>(OnBlockerToggled); //Starlight;
        SubscribeLocalEvent<BlockingComponent, PowerCellSlotEmptyEvent>(OnPowerCellEmpty); //Starlight;
        SubscribeLocalEvent<BlockingComponent, PowerCellChangedEvent>(OnPowerCellChanged); //Starlight;
    }

    #region Starlight
    /// <summary>
    /// If power cell is empty,the shield should be disabled
    /// </summary>
    private void OnPowerCellEmpty(EntityUid uid, BlockingComponent component, PowerCellSlotEmptyEvent args) => TryDeactivate(uid);
    /// <summary>
    /// If power cell is swapped,the shield should be disabled
    /// </summary>
    private void OnPowerCellChanged(EntityUid uid, BlockingComponent component, PowerCellChangedEvent args)
    {
        if (args.Ejected)
            TryDeactivate(uid);
    }
    /// <summary>
    /// If an event triggers that wishes to turn off the shield, this helper function does so
    /// </summary>
    private void TryDeactivate(EntityUid uid)
    {
        if (!HasComp<BlockingComponent>(uid))
            return;
        if (!TryComp<ItemToggleComponent>(uid, out var itemToggle) || !itemToggle.Activated)
            return;
        _itemToggle.TryDeactivate(uid, predicted: false);
    }
    /// <summary>
    /// stop user from blocking when the shield is toggled off
    /// </summary>
    private void OnBlockerToggled(EntityUid uid, BlockingComponent component, ItemToggledEvent args)
    {
        if (!args.Activated && component.IsBlocking && TryComp<BlockingUserComponent>(component.User, out var blockingUserComponent) && TryComp<TransformComponent>(uid, out var transform))
            UserStopBlocking(transform.ParentUid, blockingUserComponent);
    }
    #endregion

    private void OnParentChanged(EntityUid uid, BlockingUserComponent component, ref EntParentChangedMessage args)
    {
        UserStopBlocking(uid, component);
    }

    private void OnInsertAttempt(EntityUid uid, BlockingUserComponent component, ContainerGettingInsertedAttemptEvent args)
    {
        UserStopBlocking(uid, component);
    }

    private void OnAnchorChanged(EntityUid uid, BlockingUserComponent component, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            return;

        UserStopBlocking(uid, component);
    }

    private void OnUserDamageModified(EntityUid uid, BlockingUserComponent component, DamageModifyEvent args)
    {
        if (component.BlockingItem is not { } item || !TryComp<BlockingComponent>(item, out var blocking))
            return;

        if (args.Damage.GetTotal() <= 0)
            return;

        #region Starlight
        // A shield that needs to be toggled to function should only absorb damage if it is toggled
        if (TryComp<ItemToggleComponent>(item, out var itemToggle) && !itemToggle.Activated)
            return;

        #endregion

        // A shield should only block damage it can itself absorb. To determine that we need the Damageable component on it.
        if (!TryComp<DamageableComponent>(item, out var dmgComp))
            return;

        var blockFraction = blocking.IsBlocking ? blocking.ActiveBlockFraction : blocking.PassiveBlockFraction;
        blockFraction = Math.Clamp(blockFraction, 0, 1);

        #region Starlight
        // A shield that uses power to function needs to use that power
        if (!TryComp<PowerCellSlotComponent>(item, out var powerCellComp))
        {
            _damageable.TryChangeDamage((item, dmgComp), blockFraction * args.OriginalDamage); //Original Wizden code, this should be applicable in the majority of cases
        }
        else //if the shield has a battery slot, then we consume charge not durability
        {
            var damageMod = blocking.IsBlocking ? blocking.ActiveBlockDamageModifier : blocking.PassiveBlockDamageModifer;
            var damage = DamageSpecifier.ApplyModifierSet(args.Damage, damageMod);
            var damageEnergy = (float) damage.GetTotal() * blocking.DamageEnergyDraw * blockFraction;

            var availableEnergy = _powerCell.GetRemainingUses(item, 1f);
            if (availableEnergy <= 0)
                return; // If the power cell is empty, no damage will be blocked

            var energyUsed = damageEnergy;
            if (damageEnergy > availableEnergy)
            {
                energyUsed = availableEnergy;
                blockFraction *= energyUsed / damageEnergy; //reduce block fraction if there wasn't enough energy to actually block the damage fully
            }

            if (!_powerCell.TryUseCharge(item, energyUsed))
                return; // if no battery or no charge, doesn't work and all damage is applied
        }
        #endregion

        var modify = new DamageModifierSet();
        foreach (var key in dmgComp.Damage.DamageDict.Keys)
        {
            modify.Coefficients.TryAdd(key, 1 - blockFraction);
        }

        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modify);

        if (blocking.IsBlocking && !args.Damage.Equals(args.OriginalDamage))
        {
            _audio.PlayPvs(blocking.BlockSound, uid);
        }
    }

    private void OnDamageModified(EntityUid uid, BlockingComponent component, DamageModifyEvent args)
    {
        var modifier = component.IsBlocking ? component.ActiveBlockDamageModifier : component.PassiveBlockDamageModifer;
        if (modifier == null)
        {
            return;
        }

        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifier);
    }

    private void OnEntityTerminating(EntityUid uid, BlockingUserComponent component, ref EntityTerminatingEvent args)
    {
        if (!TryComp<BlockingComponent>(component.BlockingItem, out var blockingComponent))
            return;

        StopBlockingHelper(component.BlockingItem.Value, blockingComponent, uid);

    }

    /// <summary>
    /// Check for the shield and has the user stop blocking
    /// Used where you'd like the user to stop blocking, but also don't want to remove the <see cref="BlockingUserComponent"/>
    /// </summary>
    /// <param name="uid">The user blocking</param>
    /// <param name="component">The <see cref="BlockingUserComponent"/></param>
    private void UserStopBlocking(EntityUid uid, BlockingUserComponent component)
    {
        if (TryComp<BlockingComponent>(component.BlockingItem, out var blockComp) && blockComp.IsBlocking)
            StopBlocking(component.BlockingItem.Value, blockComp, uid);
    }
}
