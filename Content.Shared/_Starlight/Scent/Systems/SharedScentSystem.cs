using Content.Shared._Starlight.Scent.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Starlight.Scent.Systems;

// See ClientScentSystem for the concrete client-side subclass.
public abstract class SharedScentSystem : EntitySystem
{
    [Dependency] protected readonly SharedActionsSystem Actions = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] protected readonly MobStateSystem MobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmellerComponent, MapInitEvent>(OnSmellerMapInit);
        SubscribeLocalEvent<SmellerComponent, ComponentShutdown>(OnSmellerShutdown);
        SubscribeLocalEvent<SmellerComponent, ToggleSniffActionEvent>(OnToggleSniff);
        SubscribeLocalEvent<SmellerComponent, SneezeActionEvent>(OnSneeze);
    }

    private void OnSmellerMapInit(Entity<SmellerComponent> ent, ref MapInitEvent args)
    {
        Actions.AddAction(ent.Owner, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
    }

    private void OnSmellerShutdown(Entity<SmellerComponent> ent, ref ComponentShutdown args)
    {
        Actions.RemoveAction(ent.Owner, ent.Comp.ToggleActionEntity);
        Actions.RemoveAction(ent.Owner, ent.Comp.SneezeActionEntity);
        Actions.RemoveAction(ent.Owner, ent.Comp.SniffObjectActionEntity);
    }

    private void OnToggleSniff(Entity<SmellerComponent> ent, ref ToggleSniffActionEvent args)
    {
        if (args.Handled)
            return;

        SetSniffing(ent, !ent.Comp.Sniffing);
        args.Handled = true;
    }

    private void OnSneeze(Entity<SmellerComponent> ent, ref SneezeActionEvent args)
    {
        if (args.Handled)
            return;

        Sneeze(ent, predicted: true);
        args.Handled = true;
    }

    // predicted excludes the performer from the server's broadcast; ForceSneeze doesn't want that.
    public void Sneeze(Entity<SmellerComponent> ent, bool predicted = false)
    {
        ClearTrackedScent(ent);
        Audio.PlayPredicted(ent.Comp.SneezeSound, ent.Owner, predicted ? ent.Owner : null);

        var message = Loc.GetString("scent-sneeze-popup");
        if (predicted)
            Popup.PopupClient(message, ent.Owner, ent.Owner);
        else
            Popup.PopupEntity(message, ent.Owner, ent.Owner);
    }

    // Not the voluntary sneeze action. Locks out Toggle Smelling for 'lockout'.
    public void ForceSneeze(Entity<SmellerComponent> ent, TimeSpan lockout)
    {
        if (MobState.IsDead(ent.Owner))
            return;

        if (TryComp<ActionComponent>(ent.Comp.ToggleActionEntity, out var toggleAction) &&
            Actions.IsCooldownActive(toggleAction))
        {
            Actions.SetCooldown(ent.Comp.ToggleActionEntity, lockout);
            return;
        }

        Sneeze(ent);
        SetSniffing(ent, false);
        Actions.SetCooldown(ent.Comp.ToggleActionEntity, lockout);
    }

    public void SetTrackedScent(Entity<SmellerComponent> ent, string scentId)
    {
        if (ent.Comp.TrackedScentId == scentId)
            return;

        var hadTracked = ent.Comp.TrackedScentId != null;
        ent.Comp.TrackedScentId = scentId;

        if (!hadTracked)
            Actions.AddAction(ent.Owner, ref ent.Comp.SneezeActionEntity, ent.Comp.SneezeAction);

        Dirty(ent);
    }

    public void ClearTrackedScent(Entity<SmellerComponent> ent)
    {
        if (ent.Comp.TrackedScentId == null)
            return;

        ent.Comp.TrackedScentId = null;
        Actions.RemoveAction(ent.Owner, ent.Comp.SneezeActionEntity);
        Dirty(ent);
    }

    // Sneeze action grant/revoke happens in SetTrackedScent/ClearTrackedScent instead.
    public virtual void SetSniffing(Entity<SmellerComponent> ent, bool sniffing)
    {
        if (ent.Comp.Sniffing == sniffing)
            return;

        ent.Comp.Sniffing = sniffing;
        Actions.SetToggled(ent.Comp.ToggleActionEntity, sniffing);

        if (sniffing)
            Actions.AddAction(ent.Owner, ref ent.Comp.SniffObjectActionEntity, ent.Comp.SniffObjectAction);
        else
            Actions.RemoveAction(ent.Owner, ent.Comp.SniffObjectActionEntity);

        Dirty(ent);
    }

    public virtual void RandomizeScent(Entity<ScentComponent?> ent) { }
}
