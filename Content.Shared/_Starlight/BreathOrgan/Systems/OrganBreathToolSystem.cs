using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Interaction;
using Content.Shared.UserInterface;
using Content.Shared._Starlight.BreathOrgan.Components;
using Content.Shared.Body.Systems;

namespace Content.Shared._Starlight.BreathOrgan.Systems;

public sealed class OrganBreathToolSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedGasTankSystem _gasTank = default!;
    [Dependency] private readonly SharedInternalsSystem _internals = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrganBreathToolComponent, OrganAddedToBodyEvent>(OnOrganBreathToolAddedToBody);
        SubscribeLocalEvent<OrganBreathToolComponent, OrganRemovedFromBodyEvent>(OnOrganBreathToolRemovedFromBody);
        SubscribeLocalEvent<BodyComponent, OpenUiActionEvent>(OnBodyOpenUiAction);
        SubscribeLocalEvent<OrganBreathToolComponent, AccessibleOverrideEvent>(OnOrganAccessibleOverride);
    }

    /// <summary>
    ///  This method is needed for us to be able to affect a tank that is inside of our body, normally not accessible
    /// </summary>
    private void OnOrganAccessibleOverride(Entity<OrganBreathToolComponent> ent, ref AccessibleOverrideEvent args)
    {
        if (args.Handled || args.Accessible || args.Target != ent.Owner)
            return;

        if (TryComp<OrganComponent>(ent.Owner, out var organ) && organ.Body == args.User)
        {
            args.Accessible = true;
            args.Handled = true;
        }
    }

    /// <summary>
    /// Show UI for the gas tank
    /// </summary>
    private void OnBodyOpenUiAction(Entity<BodyComponent> body, ref OpenUiActionEvent args)
    {
        if (args.Key == null || !args.Key.Equals(SharedGasTankUiKey.OrganKey))
            return;

        var organTanks = _body.GetBodyOrganEntityComps<GasTankComponent>((body.Owner, body.Comp));
        foreach (var organTank in organTanks)
        {
            if (HasComp<OrganBreathToolComponent>(organTank.Owner) &&
                HasComp<UserInterfaceComponent>(organTank.Owner))
            {
                args.Handled = _ui.TryToggleUi(organTank.Owner, args.Key, args.Performer);
                return;
            }
        }
    }

    /// <summary>
    /// Initialize the organ
    /// </summary>
    private void OnOrganBreathToolAddedToBody(Entity<OrganBreathToolComponent> ent, ref OrganAddedToBodyEvent args)
    {
        // Add breath tool to the body, allows us to breathe without a mask
        if (TryComp<BreathToolComponent>(ent.Owner, out var breathTool) &&
            TryComp(args.Body, out InternalsComponent? internals))
        {
            breathTool.ConnectedInternalsEntity = args.Body;
            _internals.ConnectBreathTool((args.Body, internals), ent.Owner);
        }


        if (TryComp<GasTankComponent>(ent.Owner, out var gasTank))
        {
            var actionsComp = EnsureComp<ActionsComponent>(args.Body);

            // Add action to toggle internals
            _actionContainer.EnsureAction(ent.Owner, ref gasTank.ToggleActionEntity, ent.Comp.ToggleAction);

            if (gasTank.ToggleActionEntity != null && TryComp<ActionsContainerComponent>(ent.Owner, out var actionContainer))
            {
                _actions.AddAction((args.Body, actionsComp), gasTank.ToggleActionEntity.Value, (ent.Owner, actionContainer));
            }

            // Add action to view the gas tank UI
            _actionContainer.EnsureAction(ent.Owner, ref ent.Comp.ViewGasTankActionEntity, ent.Comp.ViewGasTankAction);

            if (ent.Comp.ViewGasTankActionEntity != null && TryComp<ActionsContainerComponent>(ent.Owner, out var actionContainer2))
            {
                _actions.AddAction((args.Body, actionsComp), ent.Comp.ViewGasTankActionEntity.Value, (ent.Owner, actionContainer2));
            }

            Dirty(ent);

            // If organ is intended to start activated, immediately turn on internals, e.g. Vox lungs so they don't die.
            if (ent.Comp.StartActivated && TryComp(args.Body, out InternalsComponent? bodyInternals) && bodyInternals.BreathTools.Count > 0)
            {
                _gasTank.ConnectToInternals((ent.Owner, gasTank), user: args.Body);
            }
        }
    }

    /// <summary>
    /// Deinitialize the organ
    /// </summary>
    private void OnOrganBreathToolRemovedFromBody(Entity<OrganBreathToolComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        // Remove the breathing tool
        if (TryComp<BreathToolComponent>(ent.Owner, out var breathTool))
        {
            _atmos.DisconnectInternals((ent.Owner, breathTool), forced: true);
        }

        if (TryComp<GasTankComponent>(ent.Owner, out var gasTank) &&
            TryComp<ActionsComponent>(args.OldBody, out var actionsComp))
        {
            // Remove the toggle internals button
            if (gasTank.ToggleActionEntity != null)
            {
                _actions.RemoveAction((args.OldBody, actionsComp), gasTank.ToggleActionEntity.Value);
            }

            // Remove the gas tank UI preview button
            if (ent.Comp.ViewGasTankActionEntity != null)
            {
                _actions.RemoveAction((args.OldBody, actionsComp), ent.Comp.ViewGasTankActionEntity.Value);
            }
        }

        Dirty(ent);
    }
}

