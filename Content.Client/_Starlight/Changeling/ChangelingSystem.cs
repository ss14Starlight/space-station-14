using Content.Client.Alerts;
using Content.Client.UserInterface.Systems.Alerts.Controls;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Examine;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Client._Starlight.Changeling;

public sealed partial class ChangelingSystem : EntitySystem
{

    [Dependency] private IPrototypeManager _prototype = default!;
    private bool _colonyInputChanged;
    public override void Initialize()
    {
        base.Initialize();

        InitializeMindspace();
    }

    [SubscribeLocalEvent]
    private void OnColonyState(Entity<ChangelingColonyMemberComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ConfigureColonyControls(ent);
        UpdateSprite(ent);
    }

    [SubscribeLocalEvent]
    private void OnColonyStartup(Entity<ChangelingColonyMemberComponent> ent, ref ComponentStartup args)
    {
        ConfigureColonyControls(ent);
        UpdateSprite(ent);
    }

    [SubscribeLocalEvent]
    private void OnColonyPlayerAttached(Entity<ChangelingColonyMemberComponent> ent, ref LocalPlayerAttachedEvent args)
        => ConfigureColonyControls(ent);

    private void ConfigureColonyControls(Entity<ChangelingColonyMemberComponent> ent)
    {
        if (!ent.Comp.IsFragment || ent.Comp.IsBlood)
            return;
        // These components are local, so adding the mob template on the server does not grant them here.
        EnsureComp<ExaminerComponent>(ent);
        // Detached heads normally use the ghost input context; living parts need ordinary mob controls.
        var input = EnsureComp<InputComponent>(ent);
        input.ContextName = "human";
        if (_hivePlayer.LocalEntity == ent.Owner)
            _colonyInputChanged = true;
    }

    [SubscribeLocalEvent]
    private void OnUpdateAlert(EntityUid uid, ChangelingComponent comp, ref UpdateAlertSpriteEvent args)
    {
        var stateNormalized = 0f;

        // hardcoded because uhh umm i don't know. send help.
        switch (args.Alert.AlertKey.AlertType)
        {
            case "ChangelingChemicals":
                stateNormalized = (int)(comp.Chemicals / comp.MaxChemicals * 18);
                break;

            case "ChangelingBiomass":
                stateNormalized = (int)(comp.Biomass / comp.MaxBiomass * 16);
                break;
            default:
                return;
        }
        var sprite = args.SpriteViewEnt.Comp;
        sprite.LayerSetState(AlertVisualLayers.Base, $"{stateNormalized}");
    }

    [SubscribeLocalEvent]
    private void GetChanglingIcon(Entity<ChangelingComponent> ent, ref GetStatusIconsEvent args)
    {
        if (HasComp<HivemindComponent>(ent) && _prototype.TryIndex(ent.Comp.StatusIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
