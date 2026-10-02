using System.Linq;
using Content.Shared._Starlight.ItemSwitch.Components;
using Content.Shared._Starlight.Switchable;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Starlight.ItemSwitch;
public abstract partial class SharedItemSwitchSystem : EntitySystem
{
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private EntityQuery<ItemSwitchComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        _query = GetEntityQuery<ItemSwitchComponent>();

        SubscribeLocalEvent<ItemSwitchComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ItemSwitchComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ItemSwitchComponent, GetVerbsEvent<ActivationVerb>>(OnActivateVerb);
        SubscribeLocalEvent<ItemSwitchComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerb);
        SubscribeLocalEvent<ItemSwitchComponent, ActivateInWorldEvent>(OnActivate);

        SubscribeLocalEvent<ClothingComponent, ItemSwitchedEvent>(UpdateClothingLayer);
    }

    private void OnMapInit(Entity<ItemSwitchComponent> ent, ref MapInitEvent args)
    {
        var state = ent.Comp.State;
        state ??= ent.Comp.States.Keys.FirstOrDefault();
        if (state != null)
            Switch((ent, ent.Comp), state, predicted: ent.Comp.Predictable);
    }

    private void OnUseInHand(Entity<ItemSwitchComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !ent.Comp.OnUse || ent.Comp.States.Count == 0) return;
        args.Handled = true;

        if (ent.Comp.States.TryGetValue(Next(ent), out var state) && state.Hidden)
            return;

        Switch((ent, ent.Comp), Next(ent), args.User, predicted: ent.Comp.Predictable);
    }

    private void OnActivateVerb(Entity<ItemSwitchComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.OnActivate || ent.Comp.States.Count == 0) return;

        var user = args.User;
        int addedVerbs = 0;

        foreach (var state in ent.Comp.States)
        {
            if (state.Value.Hidden)
                continue;
            args.Verbs.Add(new ActivationVerb()
            {
                Text = Loc.TryGetString(state.Value.Verb, out var title) ? title : state.Value.Verb,
                Category = VerbCategory.Switch,
                Act = () => Switch((ent.Owner, ent.Comp), state.Key, user, ent.Comp.Predictable)
            });
            addedVerbs++;
        }

        if (addedVerbs > 0)
            args.ExtraCategories.Add(VerbCategory.Switch);
    }

    /// <summary>
    /// Offers a single verb cycling to the next state, which alt-use in hand runs directly.
    /// </summary>
    private void OnAlternativeVerb(Entity<ItemSwitchComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.OnAltUse || ent.Comp.States.Count == 0)
            return;

        var next = Next(ent);

        if (!ent.Comp.States.TryGetValue(next, out var state) || state.Hidden)
            return;

        var user = args.User;

        args.Verbs.Add(new AlternativeVerb()
        {
            Text = Loc.GetString("item-switch-verb-cycle",
                ("state", Loc.TryGetString(state.Verb, out var title) ? title : state.Verb)),
            // Alt-use runs only the first verb, so this has to outrank an item slot's eject verb at zero.
            Priority = 1,
            Act = () => Switch((ent.Owner, ent.Comp), next, user, ent.Comp.Predictable)
        });
    }

    private void OnActivate(Entity<ItemSwitchComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !ent.Comp.OnActivate)
            return;

        args.Handled = true;

        if (ent.Comp.States.TryGetValue(Next(ent), out var state) && state.Hidden)
            return;

        Switch((ent.Owner, ent.Comp), Next(ent), args.User, predicted: ent.Comp.Predictable);
    }

    private static string Next(Entity<ItemSwitchComponent> ent)
    {
        var foundCurrent = false;
        string firstState = null!;

        foreach (var state in ent.Comp.States.Keys)
        {
            firstState ??= state;

            if (foundCurrent)
                return state;

            if (state == ent.Comp.State)
                foundCurrent = true;
        }
        return firstState;
    }

    /// <summary>
    /// Used when an item is attempted to be toggled.
    /// Sets its state to the opposite of what it is.
    /// </summary>
    /// <returns>Same as <see cref="TrySetActive"/></returns>
    public bool Switch(Entity<ItemSwitchComponent?> ent, string key, EntityUid? user = null, bool predicted = true)
    {
        if (!_query.Resolve(ent, ref ent.Comp, false) || !ent.Comp.States.TryGetValue(key, out var state))
            return false;

        var uid = ent.Owner;
        var comp = ent.Comp;

        if (!comp.Predictable && _netManager.IsClient)
            return true;

        var attempt = new ItemSwitchAttemptEvent
        {
            User = user,
            State = key
        };
        RaiseLocalEvent(uid, ref attempt);

        if (!comp.Predictable) predicted = false;

        // Bail before anything is mutated. This check used to sit after the component swap, so a
        // cancelled switch left the entity holding the incoming state's components while
        // comp.State still named the outgoing one -- and, once a UI close was added here, with its
        // screen shut too. Nothing cancels this event today, but the trap was waiting for whoever
        // first did.
        if (attempt.Cancelled)
        {
            if (predicted)
                _audio.PlayPredicted(state.SoundFailToActivate, uid, user);
            else
                _audio.PlayPvs(state.SoundFailToActivate, uid);

            if (attempt.Popup != null && user != null)
                if (predicted)
                    _popup.PopupClient(attempt.Popup, uid, user.Value);
                else
                    _popup.PopupEntity(attempt.Popup, uid, user.Value);

            return false;
        }

        // Close whatever screen is open before this state's key takes over. Anyone who had it open
        // is remembered so the incoming state can reopen for them, making a switch read as the
        // screen changing rather than the device shutting off.
        var reopenFor = new List<EntityUid>();
        TryComp<ActivatableUIComponent>(uid, out var activatable);
        if (activatable?.Key != null)
        {
            reopenFor.AddRange(_ui.GetActors(uid, activatable.Key));
            _ui.CloseUi(uid, activatable.Key);
        }

        if (ent.Comp.States.TryGetValue(ent.Comp.State, out var prevState) && prevState.RemoveComponents && prevState.Components is not null)
            EntityManager.RemoveComponents(ent, prevState.Components);

        if (state.Components is not null)
            EntityManager.AddComponents(ent, state.Components);

        // Retarget the prototype-declared ActivatableUI rather than swapping the component in and
        // out with the rest of the state. See ItemSwitchState.ActivatableUiKey for why.
        if (activatable != null && state.ActivatableUiKey != null)
            activatable.Key = state.ActivatableUiKey;

        if (predicted)
            _audio.PlayPredicted(state.SoundStateActivate, uid, user);
        else
            _audio.PlayPvs(state.SoundStateActivate, uid);

        comp.State = key;
        UpdateVisuals((uid, comp), key);
        Dirty(uid, comp);

        // Reopen on the incoming state's key for whoever had the old screen up.
        if (reopenFor.Count > 0 && activatable?.Key != null)
        {
            foreach (var actor in reopenFor)
                _ui.OpenUi(uid, activatable.Key, actor);
        }

        var switched = new ItemSwitchedEvent { Predicted = predicted, State = key, User = user };
        RaiseLocalEvent(uid, ref switched);

        return true;
    }
    public virtual void VisualsChanged(Entity<ItemSwitchComponent> ent, string key)
    {

    }
    protected virtual void UpdateVisuals(Entity<ItemSwitchComponent> ent, string key)
    {
        if (TryComp(ent, out AppearanceComponent? appearance))
            _appearance.SetData(ent, SwitchableVisuals.Switched, key, appearance);
        _item.SetHeldPrefix(ent, key);

        VisualsChanged(ent, key);
    }
    private void UpdateClothingLayer(Entity<ClothingComponent> ent, ref ItemSwitchedEvent args)
        => _clothing.SetEquippedPrefix(ent, args.State, ent.Comp);
}
