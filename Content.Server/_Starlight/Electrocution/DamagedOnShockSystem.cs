using Content.Shared.Damage.Systems;

namespace Content.Server._Starlight.Electrocution;

public sealed partial class DamagedOnShockSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    private readonly List<EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamagedOnShockComponent, ElectrifiedShockDealtEvent>(OnShockDealt);
    }

    private void OnShockDealt(Entity<DamagedOnShockComponent> ent, ref ElectrifiedShockDealtEvent args)
    {
        _pending.Add(ent);
    }

    public override void Update(float frameTime)
    {
        if (_pending.Count == 0)
            return;

        foreach (var uid in _pending)
        {
            if (TerminatingOrDeleted(uid) || !TryComp<DamagedOnShockComponent>(uid, out var comp))
                continue;

            _damageable.TryChangeDamage(uid, comp.Damage, comp.IgnoreResistances, interruptsDoAfters: false);
        }

        _pending.Clear();
    }
}
