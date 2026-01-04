using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
// Starlight Start
using Content.Shared.Verbs;
// Starlight End

namespace Content.Shared.Pinpointer;

public abstract class SharedPinpointerSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly EmagSystem _emag = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PinpointerComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<PinpointerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PinpointerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PinpointerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs); // Starlight
    }

    /// <summary>
    ///     Set the target if capable
    /// </summary>
    private void OnAfterInteract(EntityUid uid, PinpointerComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target)
            return;

        if (!component.CanRetarget || component.IsActive)
            return;

        // TODO add doafter once the freeze is lifted
        args.Handled = true;
        component.Target = args.Target;
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):player} set target of {ToPrettyString(uid):pinpointer} to {ToPrettyString(component.Target.Value):target}");
        if (component.UpdateTargetName)
            component.TargetName = component.Target == null ? null : Identity.Name(component.Target.Value, EntityManager);
    }

    /// <summary>
    ///     Set pinpointers target to track
    /// </summary>
    public virtual void SetTarget(EntityUid uid, EntityUid? target, PinpointerComponent? pinpointer = null)
    {
        if (!Resolve(uid, ref pinpointer))
            return;

        if (pinpointer.Target == target)
            return;

        pinpointer.Target = target;
        if (pinpointer.UpdateTargetName)
            pinpointer.TargetName = target == null ? null : Identity.Name(target.Value, EntityManager);
        if (pinpointer.IsActive)
            UpdateDirectionToTarget(uid, pinpointer);
    }

    // Starlight Start
    /// <summary>
    ///     Set pinpointer target component and name for tracking
    /// </summary>
    public virtual void SetTarget(EntityUid uid, EntityUid? target, string? componentName, string? targetName, PinpointerComponent? pinpointer = null)
    {
        if (!Resolve(uid, ref pinpointer))
            return;

        pinpointer.Target = target;
        pinpointer.Component = componentName;
        pinpointer.TargetName = targetName;
        
        if (pinpointer.IsActive)
            UpdateDirectionToTarget(uid, pinpointer);
        
        Dirty(uid, pinpointer);
    }
    // Starlight End

    /// <summary>
    ///     Update direction from pinpointer to selected target (if it was set)
    /// </summary>
    protected virtual void UpdateDirectionToTarget(EntityUid uid, PinpointerComponent? pinpointer = null)
    {

    }

    private void OnExamined(EntityUid uid, PinpointerComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange) // Starlight Edit
            return;

        // Starlight edit Start
        var targetName = GetCurrentTargetName(component);
        if (targetName != null)
            args.PushMarkup(Loc.GetString("examine-pinpointer-linked", ("target", targetName)));
        // Starlight edit End
    }

    /// <summary>
    ///     Manually set distance from pinpointer to target
    /// </summary>
    public void SetDistance(EntityUid uid, Distance distance, PinpointerComponent? pinpointer = null)
    {
        if (!Resolve(uid, ref pinpointer))
            return;

        if (distance == pinpointer.DistanceToTarget)
            return;

        pinpointer.DistanceToTarget = distance;
        Dirty(uid, pinpointer);
    }

    /// <summary>
    ///     Try to manually set pinpointer arrow direction.
    ///     If difference between current angle and new angle is smaller than
    ///     pinpointer precision, new value will be ignored and it will return false.
    /// </summary>
    public bool TrySetArrowAngle(EntityUid uid, Angle arrowAngle, PinpointerComponent? pinpointer = null)
    {
        if (!Resolve(uid, ref pinpointer))
            return false;

        if (pinpointer.ArrowAngle.EqualsApprox(arrowAngle, pinpointer.Precision))
            return false;

        pinpointer.ArrowAngle = arrowAngle;
        Dirty(uid, pinpointer);

        return true;
    }

    /// <summary>
    ///     Activate/deactivate pinpointer screen. If it has target it will start tracking it.
    /// </summary>
    public void SetActive(EntityUid uid, bool isActive, PinpointerComponent? pinpointer = null)
    {
        if (!Resolve(uid, ref pinpointer))
            return;
        if (isActive == pinpointer.IsActive)
            return;

        pinpointer.IsActive = isActive;
        Dirty(uid, pinpointer);
    }

    /// <summary>
    ///     Toggle Pinpointer screen. If it has target it will start tracking it.
    /// </summary>
    /// <returns>True if pinpointer was activated, false otherwise</returns>
    public virtual bool TogglePinpointer(EntityUid uid, PinpointerComponent? pinpointer = null)
    {
        if (!Resolve(uid, ref pinpointer))
            return false;

        var isActive = !pinpointer.IsActive;
        SetActive(uid, isActive, pinpointer);
        return isActive;
    }

    private void OnEmagged(EntityUid uid, PinpointerComponent component, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(uid, EmagType.Interaction))
            return;

        if (component.CanRetarget)
            return;

        args.Handled = true;
        component.CanRetarget = true;
        // Starlight Start
        component.UpdateTargetName = true; // Allow updating target name when retargeting
        component.Targets = null; // Disable multi-target system when emagged
        component.CurrentTargetIndex = -1;
        // Starlight End
        _adminLogger.Add(LogType.Emag, LogImpact.Medium, $"{ToPrettyString(args.UserUid):player} emagged {ToPrettyString(uid):entity} to allow retargeting");
    }

    // Starlight Start: Mutli Target Pinpointers
    private void OnGetVerbs(EntityUid uid, PinpointerComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Only show verbs if we have multiple targets
        if (component.Targets == null || component.Targets.Count <= 1)
            return;

        // Toggle pinpointer on/off
        var toggleVerb = new AlternativeVerb
        {
            Text = component.IsActive ? Loc.GetString("pinpointer-verb-deactivate") : Loc.GetString("pinpointer-verb-activate"),
            Act = () => TogglePinpointer(uid, component),
            Priority = 2
        };
        args.Verbs.Add(toggleVerb);

        // Cycle target
        var cycleVerb = new AlternativeVerb
        {
            Text = Loc.GetString("pinpointer-verb-cycle-target"),
            Act = () => CycleTarget(uid, component),
            Priority = 1
        };
        args.Verbs.Add(cycleVerb);
    }

    public virtual void CycleTarget(EntityUid uid, PinpointerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.Targets == null || component.Targets.Count == 0)
            return;

        // Increment and wrap around
        component.CurrentTargetIndex = (component.CurrentTargetIndex + 1) % component.Targets.Count;
        
        var target = component.Targets[component.CurrentTargetIndex];
        var componentOrTag = target.Tag ?? target.Component;
        SetTarget(uid, null, componentOrTag, target.Name, component);
        
        // Update the pinpointer to find and track the new target
        Dirty(uid, component);
    }

    public string? GetCurrentTargetComponent(PinpointerComponent component)
    {
        if (component.Targets != null && component.Targets.Count > 0 && component.CurrentTargetIndex >= 0)
        {
            var target = component.Targets[component.CurrentTargetIndex];
            return target.Tag ?? target.Component;
        }
        return component.Component;
    }

    public bool IsCurrentTargetTag(PinpointerComponent component)
    {
        if (component.Targets != null && component.Targets.Count > 0 && component.CurrentTargetIndex >= 0)
        {
            return component.Targets[component.CurrentTargetIndex].Tag != null;
        }
        return false;
    }

    public string? GetCurrentTargetName(PinpointerComponent component)
    {
        if (component.Targets != null && component.Targets.Count > 0 && component.CurrentTargetIndex >= 0)
        {
            return component.Targets[component.CurrentTargetIndex].Name;
        }
        return component.TargetName;
    // Starlight End
    }
}
