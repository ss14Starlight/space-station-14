using Content.Shared._Starlight.Pinpointer.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Verbs;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Pinpointer;

public abstract partial class SharedItgResourceReclaimerSystem : EntitySystem
{
    [Dependency] private SharedInteractionSystem _interaction = null!;

    private static readonly VerbCategory _modeCategory = new("itg-resource-reclaimer-select-mode", null);

    [SubscribeLocalEvent]
    private void OnGetVerbs(Entity<ItgResourceReclaimerComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || args.Hands == null || !_interaction.InRangeUnobstructed(args.User, args.Target))
            return;

        args.Verbs.Add(CreateModeVerb(ent, ItgResourceReclaimerMode.Alive));
        args.Verbs.Add(CreateModeVerb(ent, ItgResourceReclaimerMode.Crit));
        args.Verbs.Add(CreateModeVerb(ent, ItgResourceReclaimerMode.Dead));
    }

    [SubscribeLocalEvent]
    private void OnGetAlternativeVerb(Entity<ItgResourceReclaimerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.Using.HasValue || args.Using.Value != ent.Owner)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("itg-resource-reclaimer-verb-switch-mode"),
            Act = () => SetMode(ent.Owner, GetNextMode(ent.Comp.Mode))
        });
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<ItgResourceReclaimerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("itg-resource-reclaimer-examine-mode",
            ("mode", Loc.GetString(GetModeLocalization(ent.Comp.Mode)))));
    }

    private Verb CreateModeVerb(Entity<ItgResourceReclaimerComponent> ent, ItgResourceReclaimerMode mode) =>
        new()
        {
            Text = Loc.GetString(GetModeLocalization(mode)),
            Disabled = ent.Comp.Mode == mode,
            Priority = -(int) mode,
            Category = _modeCategory,
            Act = () => SetMode(ent.Owner, mode)
        };

    private void SetMode(EntityUid uid, ItgResourceReclaimerMode mode)
    {
        if (!TryComp<ItgResourceReclaimerComponent>(uid, out var reclaimer) || reclaimer.Mode == mode)
            return;

        reclaimer.Mode = mode;
        Dirty(uid, reclaimer);
        OnModeChanged(uid, reclaimer);
    }

    protected virtual void OnModeChanged(EntityUid uid, ItgResourceReclaimerComponent reclaimer)
    {
    }

    private static ItgResourceReclaimerMode GetNextMode(ItgResourceReclaimerMode mode) =>
        mode switch
        {
            ItgResourceReclaimerMode.Alive => ItgResourceReclaimerMode.Crit,
            ItgResourceReclaimerMode.Crit => ItgResourceReclaimerMode.Dead,
            _ => ItgResourceReclaimerMode.Alive
        };

    private static string GetModeLocalization(ItgResourceReclaimerMode mode) =>
        mode switch
        {
            ItgResourceReclaimerMode.Alive => "itg-resource-reclaimer-mode-alive",
            ItgResourceReclaimerMode.Crit => "itg-resource-reclaimer-mode-crit",
            ItgResourceReclaimerMode.Dead => "itg-resource-reclaimer-mode-dead",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
}
