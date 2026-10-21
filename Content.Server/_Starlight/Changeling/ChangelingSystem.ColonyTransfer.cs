using System.Linq;
using Content.Server.NPC.HTN;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Actions;
using Content.Shared.Changeling.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Store.Components;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    private bool TransferColony(EntityUid mind, ChangelingColonyComponent colony, EntityUid target)
    {
        var source = colony.Active;
        if (colony.Moving || source == target || !IsLivingCarrier(target)
            || !TryComp<ChangelingComponent>(source, out var ling))
            return false;

        colony.Moving = true;
        try
        {
            EndCapture(source, Comp<ChangelingColonyMemberComponent>(source));
            _chemistryUi.CloseUi(source, ChangelingColonyUiKey.Key);
            _chemistryUi.CloseUi(source, ChangelingChemistryUiKey.Key);
            if (ling.StrainedMusclesActive)
                ToggleStrainedMuscles(source, ling);
            if (ling.StealthEnabled)
                ToggleChameleonSkin(source, ling, false);
            RemoveAllChangelingEquipment(source, ling);
            ling.Equipment.Clear();

            CopyChangelingComponent(source, target, ling);
            if (TryComp<StoreComponent>(source, out var store))
            {
                var copy = new StoreComponent();
                _serialization.CopyTo(store, ref copy, notNullableOverride: true);
                RemComp<StoreComponent>(target);
                AddComp(target, copy);
            }

            var identity = EnsureComp<ChangelingIdentityComponent>(target);
            if (TryComp<ChangelingIdentityComponent>(source, out var oldIdentity))
            {
                identity.ConsumedIdentities.AddRange(oldIdentity.ConsumedIdentities);
                if (Comp<ChangelingColonyMemberComponent>(target).IsFragment)
                    identity.CurrentIdentity = oldIdentity.CurrentIdentity;
                Dirty(target, identity);
            }
            EnsureComp<ChangelingDevourComponent>(target);
            if (!Comp<ChangelingColonyMemberComponent>(target).IsFragment)
                EnsureComp<ChangelingTransformComponent>(target);

            colony.Active = target;
            _mind.TransferTo(mind, target);

            // Move the list after PlayerDetached cleaned up its PVS overrides. Keep this body's
            // current identity so revisiting a carrier does not create another paused clone.
            if (oldIdentity != null)
            {
                oldIdentity.ConsumedIdentities.Clear();
                Dirty(source, oldIdentity);
            }
            RemComp<ChangelingTransformComponent>(source);
            if (!Comp<ChangelingColonyMemberComponent>(source).IsFragment)
                RemComp<ChangelingDevourComponent>(source);
            foreach (var action in _actions.GetActions(source).ToArray())
            {
                if (action.Comp.Container == source
                    && ling.BaseChangelingActions.Any(id => id.Id == MetaData(action).EntityPrototype?.ID))
                {
                    colony.DisabledActions.Remove(action.Owner);
                    _actions.RemoveAction(source, action.Owner);
                }
            }
            RemComp<ChangelingComponent>(source);
            RemComp<StoreComponent>(source);

            // A vacant carrier acts like the other pieces; player attachment pauses its AI.
            if (!HasComp<HTNComponent>(source) && !TerminatingOrDeleted(source))
                AddComp(source, new HTNComponent { RootTask = new HTNCompoundTask { Task = "SimpleHostileCompound" } });
            if (!TerminatingOrDeleted(source))
            {
                _factionSystem.ClearFactions(source);
                _factionSystem.AddFaction(source, ColonyFaction);
            }
            RefreshColonyFriends(target);
            RefreshFragmentActions(target, colony);
            return true;
        }
        finally
        {
            colony.Moving = false;
        }
    }
}
