using Content.Shared._Starlight.Silicons;
using Content.Shared.Body.Components;
using Content.Shared.Body.Prototypes;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Silicons;
// Todo
// Make this encompass AI too, Posi/MMI/Thaven Currently make no difference as you cant remove them from the core

public sealed partial class SiliconBrainLoadoutSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BorgChassisComponent, StartingGearEquippedEvent>(OnGearEquipped);
    }

    private void OnGearEquipped(Entity<BorgChassisComponent> ent, ref StartingGearEquippedEvent args)
    {
        if (!TryComp<AppliedRoleLoadoutComponent>(ent, out var loadoutComp))
            return;
        
        if (loadoutComp.Loadout == null)
            return;

        // Find brain effect in loadout
        foreach (var (_, selectedLoadouts) in loadoutComp.Loadout.SelectedLoadouts)
        {
            foreach (var selected in selectedLoadouts)
            {
                if (!_proto.TryIndex(selected.Prototype, out LoadoutPrototype? proto))
                    continue;

                foreach (var effect in proto.Effects)
                {
                    if (effect is SiliconBrainLoadoutEffect brainEffect)
                    {
                        InsertBrain(ent, brainEffect, loadoutComp.Profile);
                        return;
                    }
                }
            }
        }
    }

    private void InsertBrain(Entity<BorgChassisComponent> silicon, SiliconBrainLoadoutEffect effect, HumanoidCharacterProfile? profile)
    {
        if (!_container.TryGetContainer(silicon, silicon.Comp.BrainContainerId, out var container))
            return;

        _container.EmptyContainer(container);
        var coords = Transform(silicon).Coordinates;
        var brainProto = effect.BrainPrototype;
        var useMMI = effect.UseMMI;

        // If no specific brain is specified, try to get the character's species brain
        if (brainProto == null && profile != null &&
            _proto.TryIndex<SpeciesPrototype>(profile.Species, out var species) &&
            _proto.TryIndex<EntityPrototype>(species.Prototype, out var entityProto) &&
            entityProto.TryGetComponent<BodyComponent>(out var bodyComp, _compFactory) &&
            bodyComp.Prototype != null &&
            _proto.TryIndex<BodyPrototype>(bodyComp.Prototype.Value, out var body))
        {
            foreach (var (_, slot) in body.Slots)
            {
                if (slot.Organs.TryGetValue("brain", out var organProto))
                {
                    brainProto = organProto;
                    // Check if brain has BorgBrain component
                    if (brainProto != null && _proto.TryIndex<EntityPrototype>(brainProto.Value, out var brainEnt))
                        useMMI = !brainEnt.Components.ContainsKey(_compFactory.GetComponentName(typeof(BorgBrainComponent)));
                    break;
                }
            }
        }

        if (brainProto == null)
            return;

        // Spawn and insert brain
        if (useMMI)
        {
            var mmi = Spawn("MMI", coords);
            if (TryComp<MMIComponent>(mmi, out var mmiComp) && mmiComp.BrainSlot.ContainerSlot != null)
                _container.Insert(Spawn(brainProto.Value, coords), mmiComp.BrainSlot.ContainerSlot);
            _container.Insert(mmi, container);
        }
        else
            _container.Insert(Spawn(brainProto.Value, coords), container);
    }
}
