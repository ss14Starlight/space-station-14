using Content.Server.GameTicking;
using Content.Shared._NullLink;
using Content.Shared._Starlight.GameTicking.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Chemistry.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.GameTicking;
using Content.Shared.Mech.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Starlight;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server.Starlight.GameTicking;

public sealed class PeacefulRoundEndSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ISharedNullLinkPlayerRolesReqManager _rolesReq = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private bool _isEnabled = false;
    private bool _roundedEnded = false;


    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(StarlightCCVars.PeacefulRoundEnd, v => _isEnabled = v, true);

        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnded);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<GotRehydratedEvent>(OnRehydrateEvent);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        SubscribeLocalEvent<AntagonisticActionComponent, ActionValidateEvent>(OnValidateAntagonisticAction);
    }

    private void SpreadPeace(EntityUid target)
    {
        if (!_isEnabled || !_roundedEnded) return;
        if (_rolesReq.IsPeacefulBypass(target)) return;
        EnsureComp<PacifiedComponent>(target);
        EnsureComp<DisableAntagonismComponent>(target);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
        => SpreadPeace(ev.Mob);

    private void OnRehydrateEvent(ref GotRehydratedEvent ev)
        => SpreadPeace(ev.Target);

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
        => _roundedEnded = false;

    private void OnRoundEnded(RoundEndTextAppendEvent ev)
    {
        _roundedEnded = true;

        var mobMoverQuery = EntityQueryEnumerator<MobMoverComponent>();
        while (mobMoverQuery.MoveNext(out var uid, out _))
            SpreadPeace(uid);

        var mechQuery = EntityQueryEnumerator<MechComponent>();
        while (mechQuery.MoveNext(out var uid, out _))
            SpreadPeace(uid);
    }
    
    private void OnValidateAntagonisticAction(EntityUid uid, AntagonisticActionComponent component, ref ActionValidateEvent args)
    {
        if (!_isEnabled)
            return;
        if (!TryComp<DisableAntagonismComponent>(args.User, out _))
            return;
        
        _popup.PopupEntity(Loc.GetString("peaceful-round-end"), args.User, args.User, PopupType.LargeCaution);
        args.Invalid = true;
    }
}
