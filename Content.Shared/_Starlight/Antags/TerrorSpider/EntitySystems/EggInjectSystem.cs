using System.Linq;
using Content.Shared._Starlight.Antags.TerrorSpider;
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
        if (args.Cancelled || args.Handled || !_timing.IsFirstTimePredicted)
            return;
        
        args.Handled = true;

        if (args.Target.HasValue && !HasComp<HasEggHolderComponent>(args.Target.Value))
        {
            EnsureComp<EggHolderComponent>(args.Target.Value);
            EnsureComp<HasEggHolderComponent>(args.Target.Value);
            var ev = new EggsInjectedEvent();
            RaiseLocalEvent(ent, ev);
        }
    }

    private void EggInjection(EggInjectionEvent ev)
    {
        if (ev.Handled)
            return;

        ev.Handled = true;
        
        if (HasComp<HasEggHolderComponent>(ev.Target))
        {
            _popup.PopupEntity("The target already contains eggs.", ev.Performer);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ev.Performer, TimeSpan.FromSeconds(6), new EggInjectionDoAfterEvent(), ev.Performer, ev.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1f
        };

        _doAfter.TryStartDoAfter(doAfter);
    }
}
