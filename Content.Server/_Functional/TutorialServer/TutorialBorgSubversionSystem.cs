using System.Numerics;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Lock;
using Content.Shared.Wires;
using Robust.Shared.Prototypes;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Drives the cyborg tutorial subversion beat: an NPC access-breaker unlocks the
/// chassis, opens the maintenance panel, then emags so silicon laws update.
/// </summary>
public sealed partial class TutorialBorgSubversionSystem : EntitySystem
{
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedWiresSystem _wires = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TutorialServerRuleSystem _tutorial = default!;

    private static readonly EntProtoId AccessBreakerProto = "TutorialPracticeAccessBreaker";
    private static readonly EntProtoId AccessBreakerItemProto = "AccessBreakerUnlimited";
    private static readonly EntProtoId EmagProto = "Emag";

    private readonly HashSet<EntityUid> _panelOpened = new();
    private readonly HashSet<EntityUid> _emagged = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TutorialParticipantComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var part, out var xform))
        {
            if (!_tutorial.TryGetCurrentSubGoal(uid, part, out var sub))
                continue;

            switch (sub.Complete)
            {
                case TutorialStepComplete.PlayerWiresPanelOpen:
                    TryNpcAccessBreakAndOpenPanel(uid, xform);
                    break;
                case TutorialStepComplete.SiliconSubverted:
                    TryNpcEmag(uid, xform);
                    break;
            }
        }
    }

    private void TryNpcAccessBreakAndOpenPanel(EntityUid borg, TransformComponent xform)
    {
        if (_panelOpened.Contains(borg))
            return;

        if (!TryComp<WiresPanelComponent>(borg, out var panel))
            return;

        if (panel.Open)
        {
            _panelOpened.Add(borg);
            return;
        }

        var saboteur = EnsureSaboteur(borg, xform);

        // Access-break unlocks the chassis lock so the wires panel can open.
        if (TryComp<LockComponent>(borg, out var lockComp) && lockComp.Locked)
        {
            var breaker = EnsureHeldItem(saboteur, AccessBreakerItemProto);
            if (!_emag.TryEmagEffect(breaker, saboteur, borg))
                return;
        }

        if (!_wires.TogglePanel(borg, panel, open: true, user: saboteur))
            return;

        _panelOpened.Add(borg);
    }

    private void TryNpcEmag(EntityUid borg, TransformComponent xform)
    {
        if (_emagged.Contains(borg))
            return;

        if (_emag.CheckFlag(borg, EmagType.Interaction))
        {
            _emagged.Add(borg);
            return;
        }

        if (!TryComp<WiresPanelComponent>(borg, out var panel))
            return;

        var saboteur = EnsureSaboteur(borg, xform);

        if (!panel.Open)
        {
            if (TryComp<LockComponent>(borg, out var lockComp) && lockComp.Locked)
            {
                var breaker = EnsureHeldItem(saboteur, AccessBreakerItemProto);
                _emag.TryEmagEffect(breaker, saboteur, borg);
            }

            if (!_wires.TogglePanel(borg, panel, open: true, user: saboteur))
                return;
        }

        var emag = EnsureHeldItem(saboteur, EmagProto);
        if (_emag.TryEmagEffect(emag, saboteur, borg))
            _emagged.Add(borg);
    }

    private EntityUid EnsureHeldItem(EntityUid saboteur, EntProtoId proto)
    {
        if (_hands.TryGetActiveItem(saboteur, out var held) &&
            MetaData(held.Value).EntityPrototype?.ID == proto.Id)
        {
            return held.Value;
        }

        // Drop whatever is held so the scripted tool is active.
        if (held != null)
            _hands.TryDrop(saboteur, held.Value, checkActionBlocker: false);

        var item = Spawn(proto, _transform.GetMoverCoordinates(saboteur));
        _hands.TryPickupAnyHand(saboteur, item, checkActionBlocker: false);
        return item;
    }

    private EntityUid EnsureSaboteur(EntityUid borg, TransformComponent borgXform)
    {
        var mapUid = borgXform.MapUid;
        if (mapUid != null)
        {
            var query = EntityQueryEnumerator<TutorialPracticeAccessBreakerComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var xform))
            {
                if (xform.MapUid == mapUid)
                    return uid;
            }
        }

        var coords = _transform.GetMoverCoordinates(borg).Offset(new Vector2(1f, 0f));
        var saboteur = Spawn(AccessBreakerProto, coords);
        EnsureComp<TutorialPracticeAccessBreakerComponent>(saboteur);
        EnsureComp<TutorialSensorTargetComponent>(saboteur);
        return saboteur;
    }
}
