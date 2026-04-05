using Content.Server.Atmos.EntitySystems;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared.Atmos;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.Nutrition;
using Content.Shared.Radiation.Components;

namespace Content.Server._FarHorizons.Power.Generation.FissionGenerator;

public sealed partial class ReactorPartSystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedPointLightSystem _lightSystem = default!;

    private float _burnDiv => (ReactorPartBurnTemp - ReactorPartHotTemp) / 5; // The 5 is how much heat damage insulated gloves protect from

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReactorPartComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<ReactorPartComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<ReactorPartComponent, IngestedEvent>(OnIngest);

        SubscribeLocalEvent<ReactorPartComponent, AtmosExposedUpdateEvent>(OnAtmosExposed);
    }

    private void OnInit(EntityUid uid, ReactorPartComponent component, ref MapInitEvent args)
    {
        var radvalue = (component.Properties.Radioactivity * 0.1f) + (component.Properties.NeutronRadioactivity * 0.15f) + (component.Properties.FissileIsotopes * 0.125f);
        if (radvalue > 0)
        {
            var radcomp = EnsureComp<RadiationSourceComponent>(uid);
            radcomp.Intensity = radvalue;
        }

        if (component.Properties.NeutronRadioactivity > 0)
        {
            var lightcomp = _lightSystem.EnsureLight(uid);
            _lightSystem.SetEnergy(uid, component.Properties.NeutronRadioactivity, lightcomp);
            _lightSystem.SetColor(uid, Color.FromHex("#22bbff"), lightcomp);
            _lightSystem.SetRadius(uid, 1.2f, lightcomp);
        }
    }

    private void OnExamine(Entity<ReactorPartComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(ReactorPartComponent)))
        {
            switch (comp.Properties.NeutronRadioactivity)
            {
                case > 8:
                    args.PushMarkup(Loc.GetString("reactor-part-nrad-5"));
                    break;
                case > 6:
                    args.PushMarkup(Loc.GetString("reactor-part-nrad-4"));
                    break;
                case > 4:
                    args.PushMarkup(Loc.GetString("reactor-part-nrad-3"));
                    break;
                case > 2:
                    args.PushMarkup(Loc.GetString("reactor-part-nrad-2"));
                    break;
                case > 1:
                    args.PushMarkup(Loc.GetString("reactor-part-nrad-1"));
                    break;
                case > 0:
                    args.PushMarkup(Loc.GetString("reactor-part-nrad-0"));
                    break;
            }

            switch (comp.Properties.Radioactivity)
            {
                case > 8:
                    args.PushMarkup(Loc.GetString("reactor-part-rad-5"));
                    break;
                case > 6:
                    args.PushMarkup(Loc.GetString("reactor-part-rad-4"));
                    break;
                case > 4:
                    args.PushMarkup(Loc.GetString("reactor-part-rad-3"));
                    break;
                case > 2:
                    args.PushMarkup(Loc.GetString("reactor-part-rad-2"));
                    break;
                case > 1:
                    args.PushMarkup(Loc.GetString("reactor-part-rad-1"));
                    break;
                case > 0:
                    args.PushMarkup(Loc.GetString("reactor-part-rad-0"));
                    break;
            }

            if (comp.Temperature > Atmospherics.T0C + ReactorPartBurnTemp)
                args.PushMarkup(Loc.GetString("reactor-part-burning"));
            else if (comp.Temperature > Atmospherics.T0C + ReactorPartHotTemp)
                args.PushMarkup(Loc.GetString("reactor-part-hot"));
        }
    }

    private void OnIngest(Entity<ReactorPartComponent> ent, ref IngestedEvent args)
    {
        var comp = ent.Comp;
        if (comp.Properties == null)
            return;

        var properties = comp.Properties;

        if (!_entityManager.TryGetComponent<DamageableComponent>(args.Target, out var damageable) || damageable.Damage.DamageDict == null)
            return;

        var dict = damageable.Damage.DamageDict;

        var dmgKey = "Radiation";
        var dmg = (properties.NeutronRadioactivity * 20) + (properties.Radioactivity * 10) + (properties.FissileIsotopes * 5);

        if (!dict.TryAdd(dmgKey, dmg))
        {
            var prev = dict[dmgKey];
            dict.Remove(dmgKey);
            dict.Add(dmgKey, prev + dmg);
        }
    }

    private void OnAtmosExposed(EntityUid uid, ReactorPartComponent component, ref AtmosExposedUpdateEvent args)
    {
        // Stops it from cooking the room while in the reactor
        if(!TryComp(uid, out MetaDataComponent? metaData) || (metaData.Flags & MetaDataFlags.InContainer) == MetaDataFlags.InContainer)
            return;

        // Can't use args.GasMixture because then it wouldn't excite the tile
        var gasMix = _atmosphereSystem.GetContainingMixture(uid, false, true) ?? GasMixture.SpaceGas;
        if(gasMix.TotalMoles < Atmospherics.GasMinMoles)
            gasMix = GasMixture.SpaceGas;

        var DeltaT = (component.Temperature - gasMix.Temperature) * 0.01f;

        if (Math.Abs(DeltaT) < 0.1)
            return;

        component.Temperature -= DeltaT;
        if (!gasMix.Immutable) // This prevents it from heating up space itself
            // This viloates COE, but if energy is conserved, then pulling out a hot rod will instantly turn the room into an oven
            gasMix.Temperature += 0.1f * DeltaT * component.ThermalMass / _atmosphereSystem.GetHeatCapacity(gasMix, false);

        var burncomp = EnsureComp<DamageOnInteractComponent>(uid);

        burncomp.IsDamageActive = component.Temperature > Atmospherics.T0C + ReactorPartHotTemp;

        if (burncomp.IsDamageActive)
        {
            var damage = Math.Max((component.Temperature - Atmospherics.T0C - ReactorPartHotTemp) / _burnDiv, 0);

            // Giant string of if/else that makes sure it will interfere only as much as it needs to
            if (burncomp.Damage == null)
                burncomp.Damage = new() { DamageDict = new() { { "Heat", damage } } };
            else if (burncomp.Damage.DamageDict == null)
                burncomp.Damage.DamageDict = new() { { "Heat", damage } };
            else if (!burncomp.Damage.DamageDict.ContainsKey("Heat"))
                burncomp.Damage.DamageDict.Add("Heat", damage);
            else
                burncomp.Damage.DamageDict["Heat"] = damage;
        }

        Dirty(uid, burncomp);
    }
}
