using Content.Shared._Starlight.StatusIcon;
using Content.Shared.Access.Components;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;
using Content.Shared.PDA;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Access.Systems;

public abstract partial class SharedJobStatusSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private static readonly ProtoId<JobIconPrototype> JobIconForNoId = "JobIconNoId";

    public override void Initialize()
    {
        base.Initialize();

        // if the mob picks up, drops or (un)equips a pda or Id card then update their crew status
        SubscribeLocalEvent<JobStatusComponent, DidEquipEvent>((uid, comp, _) => UpdateStatus((uid, comp)));
        SubscribeLocalEvent<JobStatusComponent, DidEquipHandEvent>((uid, comp, _) => UpdateStatus((uid, comp)));
        SubscribeLocalEvent<JobStatusComponent, DidUnequipEvent>((uid, comp, _) => UpdateStatus((uid, comp)));
        SubscribeLocalEvent<JobStatusComponent, DidUnequipHandEvent>((uid, comp, _) => UpdateStatus((uid, comp)));
        SubscribeLocalEvent<FixedJobIconComponent, ComponentStartup>(OnFixedJobIconStartup); // Starlight
    }

    /// <summary>
    /// Updates this mob's job and crew status depending on their currently equipped or held pda or Id card.
    /// </summary>
    public void UpdateStatus(Entity<JobStatusComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var iconId = JobIconForNoId;

        #region Starlight
        // Entities such as K9s use a fixed job instead of an ID card.
        if (TryComp<FixedJobIconComponent>(ent, out var fixedIcon)
            && _prototype.Resolve(fixedIcon.Job, out var job))
        {
            iconId = job.Icon;
        }
        #endregion
        else if (_accessReader.FindAccessItemsInventory(ent.Owner, out var items))
        {
            foreach (var item in items)
            {
                // ID Card
                if (TryComp<IdCardComponent>(item, out var id))
                {
                    iconId = id.JobIcon;
                    break;
                }

                // PDA
                if (TryComp<PdaComponent>(item, out var pda)
                    && pda.ContainedId != null
                    && TryComp(pda.ContainedId, out id))
                {
                    iconId = id.JobIcon;
                    break;
                }
            }
        }

        ent.Comp.JobStatusIcon = iconId;
        ent.Comp.IsCrew = _prototype.Index(iconId).IsCrewJob;
        Dirty(ent);
    }
}
