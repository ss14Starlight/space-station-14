using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Item.ItemToggle; //Starlight
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Power; //Starlight
using Content.Shared.PowerCell; //Starlight
using Content.Shared.PowerCell.Components; //Starlight
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Shared.Blocking;

public sealed partial class BlockingSystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!; //Starlight
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!; //Starlight

    private void InitializeUser()
    {
        SubscribeLocalEvent<BlockingUserComponent, DamageModifyEvent>(OnUserDamageModified);
        SubscribeLocalEvent<BlockingComponent, DamageModifyEvent>(OnDamageModified);

        SubscribeLocalEvent<BlockingUserComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<BlockingUserComponent, ContainerGettingInsertedAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<BlockingUserComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<BlockingUserComponent, EntityTerminatingEvent>(OnEntityTerminating);

        SubscribeLocalEvent<BlockingUserComponent, PowerCellSlotEmptyEvent>(OnPowerCellEmpty); //Starlight;
        SubscribeLocalEvent<BlockingUserComponent, PowerCellChangedEvent>(OnPowerCellChanged); //Starlight
        SubscribeLocalEvent<BlockingUserComponent, ChargeChangedEvent>(OnChargeChanged); //Starlight
        SubscribeLocalEvent<BlockingUserComponent, PowerChangedEvent>(OnPowerChanged); //Starlight
    }

    #region Starlight
    //If power cell is empty,the shield should be disabled
    private void OnPowerCellEmpty(EntityUid uid, BlockingUserComponent component, PowerCellSlotEmptyEvent args)
    {
        TryDeactivate(component);
    }
    //If power cell is swapped,the shield should be disabled
    private void OnPowerCellChanged(EntityUid uid, BlockingUserComponent component, PowerCellChangedEvent args)
    {
        if (args.Ejected)
            TryDeactivate(component);
    }
    //If power battery runs dead, the shield should be disabled
    private void OnChargeChanged(EntityUid uid, BlockingUserComponent component, ChargeChangedEvent args)
    {
        if (args.CurrentCharge == 0)
            TryDeactivate(component);
    }
    //If power cell is swapped,the shield should be disabled
    private void OnPowerChanged(EntityUid uid, BlockingUserComponent component, PowerChangedEvent args)
    {
        TryDeactivate(component);
    }
    private bool TryDeactivate(BlockingUserComponent component)
    {
        if (component.BlockingItem is not { } item || !HasComp<BlockingComponent>(item))
            return false;

        if (TryComp<ItemToggleComponent>(item, out var itemToggle))
        {
            if (!itemToggle.Activated)
                return false;
        }
        else
        {
            return false;
        }
        return _itemToggle.TryDeactivate(item, predicted: false);
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
        if (TryComp<ItemToggleComponent>(item, out var itemToggle))
            if (!itemToggle.Activated)
                return;
        //Starlight End
        #endregion

        // A shield should only block damage it can itself absorb. To determine that we need the Damageable component on it.
        if (!TryComp<DamageableComponent>(item, out var dmgComp))
            return;

        var blockFraction = blocking.IsBlocking ? blocking.ActiveBlockFraction : blocking.PassiveBlockFraction;
        blockFraction = Math.Clamp(blockFraction, 0, 1);

        #region Starlight
        // A shield that uses power to function needs to use that power
        if (!TryComp<PowerCellSlotComponent>(item, out var slot))
        {
            _damageable.TryChangeDamage((item, dmgComp), blockFraction * args.OriginalDamage); //Original Wizden code, this should be applicable in the majority of cases
        }
        else //if the shield has a battery slot, then we consume charge not durability
        {
            var damageEnergy = blockFraction * (float) args.OriginalDamage.GetTotal() * blocking.DamageEnergyDraw;
            var availableEnergy = _powerCell.GetRemainingUses(item, 1f);
            if (availableEnergy <= 0)
            {
                TryDeactivate(component);
                return; //If the power cell is empty, no damage will be blocked
            }

            var energyUsed = damageEnergy;
            if (damageEnergy > availableEnergy)
            {
                energyUsed = availableEnergy;
                blockFraction *= energyUsed / damageEnergy; //reduce block fraction if there wasn't enough energy to actually block the damage fully
            }

            if (!_powerCell.TryUseCharge(item, energyUsed, user: uid))
                return; // if no battery or no charge, doesn't work and all damage is applied
        }
        //Starlight End
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
