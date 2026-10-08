using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Shared._Starlight.CosmicCult;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.CosmicCult.Components.Examine;
using Content.Shared._Starlight.NullSpace.Components;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.CosmicCult.Abilities;

public sealed partial class CosmicLapseSystem : EntitySystem
{
    [Dependency] private CosmicCultSystem _cult = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private static readonly ProtoId<PolymorphPrototype> _humanLapse = "CosmicLapseMobHuman";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicCultComponent, EventCosmicLapse>(OnCosmicLapse);
    }

    private void OnCosmicLapse(Entity<CosmicCultComponent> uid, ref EventCosmicLapse action)
    {
        if (action.Handled || HasComp<CosmicBlankComponent>(action.Target))
        {
            _popup.PopupEntity(Loc.GetString("cosmicability-generic-fail"), uid, uid);
            return;
        }

        foreach (var entity in _lookup.GetEntitiesIntersecting(Transform(uid).Coordinates))
            if (HasComp<NullSpaceBlockerComponent>(entity))
            {
                _popup.PopupEntity(Loc.GetString("cosmicability-generic-fail"), uid, uid);
                return;
            }

        action.Handled = true;
        var tgtpos = Transform(action.Target).Coordinates;
        Spawn(uid.Comp.LapseVFX, tgtpos);
        _popup.PopupEntity(Loc.GetString("cosmicability-lapse-success", ("target", Identity.Entity(action.Target, EntityManager))), uid, uid);
        ProtoId<PolymorphPrototype> polymorphId = _humanLapse;
        if (TryComp<CosmicLapseFormComponent>(action.Target, out var lapseForm)
            && _prototype.HasIndex(lapseForm.Form))
        {
            polymorphId = lapseForm.Form;
        }

        if (polymorphId == _humanLapse
            && TryComp<HumanoidAppearanceComponent>(action.Target, out var appearance))
        {
            ProtoId<PolymorphPrototype> speciesPolymorphId = "CosmicLapseMob" + appearance.Species;
            if (_prototype.HasIndex(speciesPolymorphId))
                polymorphId = speciesPolymorphId;
        }

        if (polymorphId == _humanLapse
            && TryComp<InventoryComponent>(action.Target, out var inventory)
            && inventory.SpeciesId is { Length: > 0 } speciesId)
        {
            ProtoId<PolymorphPrototype> speciesPolymorphId = "CosmicLapseMob"
                + char.ToUpperInvariant(speciesId[0])
                + speciesId.Substring(1);
            if (_prototype.HasIndex(speciesPolymorphId))
                polymorphId = speciesPolymorphId;
        }

        _polymorph.PolymorphEntity(action.Target, polymorphId);
        _cult.MalignEcho(uid);
    }
}
