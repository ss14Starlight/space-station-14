using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared._Sol.Medical.Virology.Events;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Physical payload deployment for bioterror cultures (food / surface / aerosol).
/// Free antagonist culture charges have been removed.
/// </summary>
public sealed class BioterrorSystem : EntitySystem
{
    [Dependency] private readonly PathogenSystem _pathogen = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PathogenPayloadComponent, AfterInteractEvent>(OnPayloadInteract);
        SubscribeLocalEvent<PathogenPayloadComponent, GetVerbsEvent<AlternativeVerb>>(OnPayloadVerbs);
    }

    private void OnPayloadVerbs(Entity<PathogenPayloadComponent> payload, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || payload.Comp.Used)
            return;

        if (payload.Comp.Kind == PathogenPayloadKind.Aerosol)
        {
            var user = args.User;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("sol-bioterror-deploy-aerosol"),
                Act = () => DeployAerosol(payload, user),
                Priority = 10,
            });
        }
    }

    private void OnPayloadInteract(Entity<PathogenPayloadComponent> payload, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target == null || args.Handled || payload.Comp.Used)
            return;

        switch (payload.Comp.Kind)
        {
            case PathogenPayloadKind.Food:
                if (!HasComp<EdibleComponent>(args.Target) && !HasComp<SurfaceContaminationComponent>(args.Target))
                    return;
                DeployFoodOrSurface(payload, args.Target.Value, args.User, PathogenPayloadKind.Food);
                args.Handled = true;
                break;
            case PathogenPayloadKind.Surface:
                DeployFoodOrSurface(payload, args.Target.Value, args.User, PathogenPayloadKind.Surface);
                args.Handled = true;
                break;
            case PathogenPayloadKind.Aerosol:
                // Verb-driven; allow click-self as release.
                if (args.Target == args.User)
                {
                    DeployAerosol(payload, args.User);
                    args.Handled = true;
                }
                break;
        }
    }

    public void DeployFoodOrSurface(
        Entity<PathogenPayloadComponent> payload,
        EntityUid target,
        EntityUid user,
        PathogenPayloadKind kind)
    {
        if (payload.Comp.Used || string.IsNullOrEmpty(payload.Comp.StrainId))
            return;

        if (!_pathogen.TryResolvePathogen(payload.Comp.StrainId, out var strain) || strain == null)
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-payload-invalid"), payload, user);
            return;
        }

        if (!_pathogen.IsVirologyEnabledAt(target) && !_pathogen.IsVirologyEnabledAt(user))
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-no-station"), user, user);
            return;
        }

        var load = payload.Comp.Concentration;
        _pathogen.AddOrIncreaseContamination(target, payload.Comp.StrainId, load);
        ConsumePayload(payload, user, kind, target, load);

        var msg = kind == PathogenPayloadKind.Food
            ? "sol-bioterror-food-contaminated"
            : "sol-bioterror-surface-contaminated";
        _popup.PopupEntity(Loc.GetString(msg), target, user);

        if (!HasComp<BioterroristComponent>(user))
            _pathogen.TryExpose(user, payload.Comp.StrainId, load * 0.2f, PathogenTransmission.Contact, payload);
    }

    public void DeployAerosol(Entity<PathogenPayloadComponent> payload, EntityUid user)
    {
        if (payload.Comp.Used || string.IsNullOrEmpty(payload.Comp.StrainId))
            return;

        if (!_pathogen.TryResolvePathogen(payload.Comp.StrainId, out _) )
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-payload-invalid"), payload, user);
            return;
        }

        if (!_pathogen.TryGetVirologyStation(user, out _, out var station) || !station.AllowAirborne)
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-no-station"), user, user);
            return;
        }

        var load = payload.Comp.Concentration;
        EntityManager.System<GridPathogenAtmosphereSystem>().AddAirborneLoad(user, payload.Comp.StrainId, load);
        ConsumePayload(payload, user, PathogenPayloadKind.Aerosol, null, load);
        _popup.PopupEntity(Loc.GetString("sol-bioterror-airborne-released"), user, user);

        if (!HasComp<BioterroristComponent>(user))
            _pathogen.TryExpose(user, payload.Comp.StrainId, load * 0.35f, PathogenTransmission.Airborne, payload);
    }

    private void ConsumePayload(
        Entity<PathogenPayloadComponent> payload,
        EntityUid user,
        PathogenPayloadKind kind,
        EntityUid? target,
        float load)
    {
        payload.Comp.Used = true;
        Dirty(payload);
        var ev = new BioterrorPayloadDeployedEvent(payload.Comp.StrainId, kind, load, user, target);
        RaiseLocalEvent(ref ev);
        QueueDel(payload.Owner);
    }
}
