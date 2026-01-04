using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.VanguardSuit;

public abstract class SharedHandcannonDeploymentSystem : EntitySystem
{
    [Dependency] protected readonly SharedActionsSystem Actions = default!;
    [Dependency] protected readonly SharedDoAfterSystem DoAfter = default!;
    [Dependency] protected readonly SharedHandsSystem Hands = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandcannonDeploymentComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HandcannonDeploymentComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HandcannonDeploymentComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<HandcannonDeploymentComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<HandcannonDeploymentComponent, DeployHandcannonActionEvent>(OnDeployAction);
        SubscribeLocalEvent<HandcannonDeploymentComponent, HandcannonDeployDoAfterEvent>(OnDeployDoAfter);
    }

    private void OnMapInit(Entity<HandcannonDeploymentComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.DeployAction != null)
            Actions.AddAction(ent, ref ent.Comp.DeployActionEntity, ent.Comp.DeployAction);
    }

    private void OnEquipped(Entity<HandcannonDeploymentComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Wearer;
        Dirty(ent);
    }

    private void OnUnequipped(Entity<HandcannonDeploymentComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        ent.Comp.Wearer = null;
        Dirty(ent);
    }

    private void OnGetActions(Entity<HandcannonDeploymentComponent> ent, ref GetItemActionsEvent args)
    {
        if (ent.Comp.DeployAction != null)
            args.AddAction(ref ent.Comp.DeployActionEntity, ent.Comp.DeployAction);
    }

    private void OnDeployAction(Entity<HandcannonDeploymentComponent> ent, ref DeployHandcannonActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Wearer == null)
            return;

        // Check if they have free hands
        if (!Hands.TryGetEmptyHand(ent.Comp.Wearer.Value, out _))
        {
            Popup.PopupEntity(Loc.GetString("handcannon-deploy-no-hands"), ent.Comp.Wearer.Value, ent.Comp.Wearer.Value);
            return;
        }

        // Start the DoAfter
        var doAfterArgs = new DoAfterArgs(EntityManager, ent.Comp.Wearer.Value, ent.Comp.DeployDelay, new HandcannonDeployDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false
        };

        DoAfter.TryStartDoAfter(doAfterArgs);
        Popup.PopupEntity(Loc.GetString("handcannon-deploy-start"), ent.Comp.Wearer.Value, ent.Comp.Wearer.Value);

        args.Handled = true;
    }

    private void OnDeployDoAfter(Entity<HandcannonDeploymentComponent> ent, ref HandcannonDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (ent.Comp.Wearer == null)
            return;

        SpawnHandcannon(ent, ent.Comp.Wearer.Value);
        args.Handled = true;
    }

    protected abstract void SpawnHandcannon(Entity<HandcannonDeploymentComponent> ent, EntityUid wearer);
}
