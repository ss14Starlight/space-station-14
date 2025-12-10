using System.Linq;
using Content.Shared._Starlight.Dice;
using Content.Shared.Chat;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Dice;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Dice.DestinyDice;

[Virtual]
public class SharedDestinyDiceSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager _proto = default!;
    [Dependency] protected readonly IGameTiming _timing = default!;
    [Dependency] protected readonly IComponentFactory _factory = default!;
    [Dependency] protected readonly IRobustRandom _sharedRandom = default!;
    [Dependency] protected readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] protected readonly SharedTransformSystem _transform = default!;
    [Dependency] protected readonly SharedMapSystem _map = default!;
    [Dependency] protected readonly SharedHandsSystem _hands = default!;
    [Dependency] protected readonly SharedPopupSystem _popup = default!;
    [Dependency] protected readonly SharedGameTicker _ticker = default!;
    [Dependency] protected readonly SharedVerbSystem _verb = default!;
    [Dependency] protected readonly DamageableSystem _damage = default!;
    
    protected static readonly ProtoId<DamageTypePrototype> _bluntDamageType = "Blunt";

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeAllEvent<DestinyDiceEffectExecutionEvent>(OnExecuteEffect);
    }

    private void OnExecuteEffect(DestinyDiceEffectExecutionEvent ev)
    {
        Log.Log(LogLevel.Info, $"Got event from {(_net.IsClient ? "client" : _net.IsServer ? "server" : "unknown")}");
        var uid = GetEntity(ev.Uid);
        if (!TryComp<DestinyDiceComponent>(uid, out var comp)) return;
        ExecuteEffect(ev.Effect, (uid, comp), GetEntity(ev.Roller), GetEntity(ev.Grid));
    }
    
    protected virtual void ExecuteEffect(IDestinyDiceEffect effect, Entity<DestinyDiceComponent> entity, EntityUid roller, EntityUid? grid){}
}