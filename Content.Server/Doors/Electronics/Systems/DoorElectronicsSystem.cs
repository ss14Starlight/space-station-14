using System.Linq;
using Content.Server.Doors.Electronics;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Doors.Electronics;
using Content.Shared.Doors;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared.Emag.Systems; // Starlight

namespace Content.Server.Doors.Electronics;

public sealed class DoorElectronicsSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly EmagSystem _emag = default!; // Starlight

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DoorElectronicsComponent, DoorElectronicsUpdateConfigurationMessage>(OnChangeConfiguration);
        SubscribeLocalEvent<DoorElectronicsComponent, AccessReaderConfigurationChangedEvent>(OnAccessReaderChanged);
        SubscribeLocalEvent<DoorElectronicsComponent, BoundUIOpenedEvent>(OnBoundUIOpened);
        SubscribeLocalEvent<DoorElectronicsComponent, GotEmaggedEvent>(OnGotEmagged); // Starlight
    }

    public void UpdateUserInterface(EntityUid uid, DoorElectronicsComponent component)
    {
        // var accesses = new List<ProtoId<AccessLevelPrototype>>(); // Starlight edit

        // Starlight edit Start
        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        var allLevels = new HashSet<ProtoId<AccessLevelPrototype>>();
        foreach (var group in component.AccessGroups)
        {
            if (protoMan.TryIndex(group, out AccessGroupPrototype? groupProto))
                allLevels.UnionWith(groupProto.Tags);
        }
        var possibleAccesses = allLevels.OrderBy(x => x).ToList();

        var pressedAccesses = new List<ProtoId<AccessLevelPrototype>>();
        if (TryComp<AccessReaderComponent>(uid, out var accessReader))
        {
            foreach (var accessList in accessReader.AccessLists)
                pressedAccesses.AddRange(accessList);
        }
        var state = new DoorElectronicsConfigurationState(possibleAccesses, component.AccessGroups, pressedAccesses);
        _uiSystem.SetUiState(uid, DoorElectronicsConfigurationUiKey.Key, state);
        // Starlight edit End
    }

    private void OnChangeConfiguration(
        EntityUid uid,
        DoorElectronicsComponent component,
        DoorElectronicsUpdateConfigurationMessage args)
    {
        var accessReader = EnsureComp<AccessReaderComponent>(uid);
        _accessReader.TrySetAccesses((uid, accessReader), args.AccessList);
    }

    private void OnAccessReaderChanged(
        EntityUid uid,
        DoorElectronicsComponent component,
        AccessReaderConfigurationChangedEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    private void OnBoundUIOpened(
        EntityUid uid,
        DoorElectronicsComponent component,
        BoundUIOpenedEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    // Starlight begin
    private void OnGotEmagged(EntityUid uid, DoorElectronicsComponent comp, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(uid, EmagType.Interaction, args.EmagComponent))
            return;

        if (args.EmagComponent is null) return;

        var addedGroups = false;
        foreach (var group in args.EmagComponent.AccessGroups.Where(group => !comp.AccessGroups.Contains(group)))
        {
            comp.AccessGroups.Add(group);
            addedGroups = true;
        }

        if (!addedGroups) return;
        args.Handled = true;
    }
    // Starlight end
}
