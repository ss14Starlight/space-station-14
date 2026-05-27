using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Humanoid;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Content.Shared.Body.Components;
using Content.Server._Starlight.Medical.Body.Systems; // Starlight

namespace Content.Server.Humanoid;

public sealed partial class HumanoidAppearanceSystem
{
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly BodySystem _body = default!; //Starlight

    private void OnVerbsRequest(EntityUid uid, HumanoidAppearanceComponent component, GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
        {
            return;
        }

        if (!_adminManager.HasAdminFlag(actor.PlayerSession, AdminFlags.Fun))
        {
            return;
        }

        args.Verbs.Add(new Verb
        {
            Text = "Modify markings",
            Category = VerbCategory.Tricks,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/Mobs/Customization/reptilian_parts.rsi"), "tail_smooth"),
            Act = () =>
            {
                _uiSystem.OpenUi(uid, HumanoidMarkingModifierKey.Key, actor.PlayerSession);
                _uiSystem.SetUiState(
                    uid,
                    HumanoidMarkingModifierKey.Key,
                    new HumanoidMarkingModifierState(component.MarkingSet, component.Species,
                        component.Sex,
                        component.SkinColor,
                        component.CustomBaseLayers
                    ));
            }
        });
    }

    //Starlight Start
    private void OnVerbsRequest(EntityUid uid, HumanoidAppearanceComponent component, GetVerbsEvent<ActivationVerb> args)
    {
        if (uid != args.Target || !TryComp(args.Target, out BodyComponent? _))
            return;

        foreach (var part in _body.GetBodyChildren(args.Target))
            RaiseLocalEvent(part.Id, args);

        foreach (var organ in _body.GetBodyOrgans(args.Target))
            RaiseLocalEvent(organ.Id, args);
    }
    //Starlight End

    private void OnBaseLayersSet(EntityUid uid, HumanoidAppearanceComponent component,
        HumanoidMarkingModifierBaseLayersSetMessage message)
    {
        if (!_adminManager.HasAdminFlag(message.Actor, AdminFlags.Fun))
        {
            return;
        }

        if (message.Info == null)
        {
            component.CustomBaseLayers.Remove(message.Layer);
        }
        else
        {
            component.CustomBaseLayers[message.Layer] = message.Info.Value;
        }

        Dirty(uid, component);

        if (message.ResendState)
        {
            _uiSystem.SetUiState(
                uid,
                HumanoidMarkingModifierKey.Key,
                new HumanoidMarkingModifierState(component.MarkingSet, component.Species,
                        component.Sex,
                        component.SkinColor,
                        component.CustomBaseLayers
                    ));
        }
    }

    private void OnMarkingsSet(EntityUid uid, HumanoidAppearanceComponent component,
        HumanoidMarkingModifierMarkingSetMessage message)
    {
        if (!_adminManager.HasAdminFlag(message.Actor, AdminFlags.Fun))
        {
            return;
        }

        component.MarkingSet = message.MarkingSet;
        Dirty(uid, component);

        if (message.ResendState)
        {
            _uiSystem.SetUiState(
                uid,
                HumanoidMarkingModifierKey.Key,
                new HumanoidMarkingModifierState(component.MarkingSet, component.Species,
                        component.Sex,
                        component.SkinColor,
                        component.CustomBaseLayers
                    ));
        }

    }
}
