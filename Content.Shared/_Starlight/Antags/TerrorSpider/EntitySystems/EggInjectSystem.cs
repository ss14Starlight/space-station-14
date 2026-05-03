using System.Linq;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Spider;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Antags.TerrorSpider;

public sealed class EggInjectSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;

    private readonly EntProtoId[] _eggs =
    [
        "TerrorRedEggSpiderFertilized",
        "TerrorGreenSpiderFertilized",
        "TerrorGrayEggSpiderFertilized"
    ];
    public override void Initialize()
    {
        SubscribeLocalEvent<EggInjectionEvent>(EggInjection);
        SubscribeLocalEvent<SpiderComponent, EggInjectionDoAfterEvent>(EggInjectionDoAfter);

        SubscribeLocalEvent<EggsLayingEvent>(EggsLaying);
        Subs.BuiEvents<TerrorPrincessComponent>(EggsLayingUiKey.Key, subs => subs.Event<EggsLayingBuiMsg>(OnEggsLaying));
    }

    private void EggsLaying(EggsLayingEvent ev)
    {
        if (ev.Handled || !_timing.IsFirstTimePredicted)
            return;

        ev.Handled = true;

        if (TryComp(ev.Performer, out ActorComponent? actor))
            _uiSystem.OpenUi(ev.Performer, EggsLayingUiKey.Key, actor.PlayerSession);
    }

    private void OnEggsLaying(EntityUid uid, TerrorPrincessComponent component, EggsLayingBuiMsg args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (_eggs.Contains(args.Egg) && TryComp(uid, out ActorComponent? actor))
        {
            SpawnAtPosition(args.Egg, Transform(uid).Coordinates);
            _uiSystem.CloseUi(uid, EggsLayingUiKey.Key, actor.PlayerSession);
        }
    }

    private void EggInjectionDoAfter(Entity<SpiderComponent> ent, ref EggInjectionDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !_timing.IsFirstTimePredicted || args.Target == null || !TryComp<WrapEntityHolderComponent>(args.Target.Value, out var wrapEntityHolder))
            return;

        if (wrapEntityHolder.Hold == null)
        {
            _popup.PopupPredicted(Loc.GetString("terror-spider-egg-inject-cocoon-empty"), ent, ent);
            return;
        }

        if (!HasComp<HasEggHolderComponent>(wrapEntityHolder.Hold.Value))
        {
            args.Handled = true;
            EnsureComp<EggHolderComponent>(wrapEntityHolder.Hold.Value);
            EnsureComp<HasEggHolderComponent>(wrapEntityHolder.Hold.Value);
            var ev = new EggsInjectedEvent();
            RaiseLocalEvent(ent, ev);
        }
        else
            _popup.PopupPredicted(Loc.GetString("terror-spider-egg-inject-already-has-eggs"), ent, ent);
    }

    private void EggInjection(EggInjectionEvent ev)
    {
        if (ev.Handled || !TryComp<WrapEntityHolderComponent>(ev.Target, out var wrapEntityHolder))
            return;

        if (wrapEntityHolder.Hold == null)
        {
            _popup.PopupPredicted(Loc.GetString("terror-spider-egg-inject-cocoon-empty"), ev.Performer, ev.Performer);
            return;
        }

        ev.Handled = true;

        if (HasComp<HasEggHolderComponent>(wrapEntityHolder.Hold.Value))
        {
            _popup.PopupPredicted(Loc.GetString("terror-spider-egg-inject-already-has-eggs"), ev.Performer, ev.Performer);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ev.Performer, TimeSpan.FromSeconds(ev.InjectionDelay), new EggInjectionDoAfterEvent(), ev.Performer, ev.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1f
        };

        _doAfter.TryStartDoAfter(doAfter);
    }
}
