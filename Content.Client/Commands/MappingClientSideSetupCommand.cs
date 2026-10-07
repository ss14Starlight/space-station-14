using Content.Client.Actions;
using Content.Client.Markers;
using Content.Client.SubFloor;
using Content.Shared.SubFloor;
using Robust.Client.Graphics;
using Robust.Shared.Console;

namespace Content.Client.Commands;

internal sealed partial class MappingClientSideSetupCommand : LocalizedEntityCommands
{
    [Dependency] private ILightManager _lightManager = default!;
    [Dependency] private ActionsSystem _actionSystem = default!;
    [Dependency] private MarkerSystem _markerSystem = default!;
    [Dependency] private SubFloorHideSystem _subfloorSystem = default!;

    public override string Command => "mappingclientsidesetup";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_lightManager.LockConsoleAccess)
            return;

        _markerSystem.MarkersVisible = true;
        _lightManager.Enabled = false;
        _subfloorSystem.SetLayer(SubFloorVisibilityMask.All, true); //Starlight edit - Subfloor layers
        _actionSystem.LoadActionAssignments("/mapping_actions.yml", false);
    }
}

