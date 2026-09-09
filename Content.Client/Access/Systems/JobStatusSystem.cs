using Content.Client.Overlays;
using Content.Shared.Access.Systems;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Access.Systems;

public sealed partial class JobStatusSystem : SharedJobStatusSystem
{
    [Dependency] private ShowJobIconsSystem _showJobIcons = default!;
    [Dependency] private ShowCrewIconsSystem _showCrewIcons = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private StationAiVisionSystem _vision = default!;
    [Dependency] private IPlayerManager _player = default!;

    private static readonly ProtoId<SecurityIconPrototype> CrewBorderIcon = "CrewBorderIcon";
    private static readonly ProtoId<SecurityIconPrototype> CrewUncertainBorderIcon = "CrewUncertainBorderIcon";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JobStatusComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    // show the status icons if the player has the correponding HUDs
    private void OnGetStatusIconsEvent(Entity<JobStatusComponent> ent, ref GetStatusIconsEvent ev)
    {
        var canSeeJobStatus = CanSeeJobStatus(ent); // Starlight

        if (_showJobIcons.IsActive && canSeeJobStatus && ent.Comp.JobStatusIcon != null) // Starlight
            ev.StatusIcons.Add(_prototype.Index(ent.Comp.JobStatusIcon));

        if (_showCrewIcons.IsActive && canSeeJobStatus) // Starlight
        {
            if (_showCrewIcons.UncertainCrewBorder)
                ev.StatusIcons.Add(_prototype.Index(CrewUncertainBorderIcon));
            else if (ent.Comp.IsCrew)
                ev.StatusIcons.Add(_prototype.Index(CrewBorderIcon));
        }
    }
}
