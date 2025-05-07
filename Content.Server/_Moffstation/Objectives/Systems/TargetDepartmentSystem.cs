using System.Diagnostics.CodeAnalysis;
using Content.Server._Moffstation.Objectives.Components;
using Content.Server.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles.Jobs;

namespace Content.Server._Moffstation.Objectives.Systems;

/// <summary>
/// Provides API for overrideing what department will get selected in an objective
/// </summary>
public sealed class TargetDepartmentSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TargetDepartmentComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
    }

    private void OnAfterAssign(EntityUid uid, TargetDepartmentComponent comp, ref ObjectiveAfterAssignEvent args)
    {
        if (!GetTarget(uid, out var target, comp))
            return;

        if (comp.Target != null)
            _metaData.SetEntityName(uid, GetTitle(comp.Title, comp.Target), args.Meta);
    }

    /// <summary>
    /// Sets the Target field for the title and other components to use.
    /// </summary>
    public void SetTarget(EntityUid uid, string target, TargetDepartmentComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.Target = target;
    }

    /// <summary>
    /// Gets the target from the component.
    /// </summary>
    /// <remarks>
    /// If it is null then the prototype is invalid, just return.
    /// </remarks>
    public bool GetTarget(EntityUid uid, [NotNullWhen(true)] out string? target, TargetDepartmentComponent? comp = null)
    {
        target = Resolve(uid, ref comp) ? comp.Target : null;
        return target != null;
    }

    private string GetTitle(string title, string target)
    {
        return Loc.GetString(title, ("targetName", target));
    }
}
