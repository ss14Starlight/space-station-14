using Content.Shared.Access;
using Content.Shared.Doors.Electronics;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Doors.Electronics;

public sealed class DoorElectronicsBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private DoorElectronicsConfigurationMenu? _window;

    public DoorElectronicsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DoorElectronicsConfigurationMenu>();
        _window.OnAccessChanged += UpdateConfiguration;
        // Starlight edit Start
        if (EntMan.TryGetComponent<MetaDataComponent>(Owner, out var meta))
            _window.Title = meta.EntityName;
        // Reset();
        // Starlight edit End
    }

    public override void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        base.OnProtoReload(args);

        if (!args.WasModified<AccessLevelPrototype>())
            return;

        // Starlight edit Start
        if (State is DoorElectronicsConfigurationState cast)
            _window?.UpdateState(cast.AccessList, cast.AccessGroups);
        // Starlight edit End
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        // Starlight Start
        base.UpdateState(state);
        if (state is not DoorElectronicsConfigurationState cast)
            return;
        // Starlight End
        _window?.UpdateState(cast.AccessList, cast.AccessGroups, cast.PressedAccessList); // Starlight edit
    }

    public void UpdateConfiguration(List<ProtoId<AccessLevelPrototype>> newAccessList)
    {
        SendMessage(new DoorElectronicsUpdateConfigurationMessage(newAccessList));
    }
}
