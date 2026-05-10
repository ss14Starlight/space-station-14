using Content.Server._Starlight.Antags;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.Antag;
using Content.Server.Emp;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Pinpointer;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed partial class TerminatorRuleSystem : GameRuleSystem<TerminatorRuleComponent>
{
    [Dependency] private readonly SharedPinpointerSystem _pinpointer = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly EmpSystem _emp = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private EntProtoId TerminatorEntityPrototype = "MobHumanTerminator";
    private EntProtoId PinpointerPrototype = "PinpointerTerminator";
    private EntProtoId SpawnEffectPrototype = "EffectTerminatorChronospace";

    private const float EmpPower = 2.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerminatorRuleComponent, AntagSelectEntityEvent>(OnAntagSelectEntity);
        SubscribeLocalEvent<TerminatorRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagEntitySelected);
    }

    private void OnAntagSelectEntity(Entity<TerminatorRuleComponent> ent, ref AntagSelectEntityEvent args)
    {
        if (args.Session?.AttachedEntity is not { } spawner) return;

        // no target set, let's just pick someone random
        if (ent.Comp.Target == null)
        {
            if (FindValidPlayer() is not Entity<MindComponent> newTarget)
            {
                Log.Warning("No minds found to make random terminator target!");
                return;
            }

            ent.Comp.Target = newTarget.Owner;
        }

        var terminator = Spawn(TerminatorEntityPrototype);
        var targetOverride = EnsureComp<TargetOverrideComponent>(terminator);
        targetOverride.Target = ent.Comp.Target;

        // give the terminator a pinpointer that is pointing toward the target
        var pinpointer = Spawn(PinpointerPrototype);
        _pinpointer.SetTarget(pinpointer, ent.Comp.TargetBody);
        _pinpointer.SetActive(pinpointer, true);
        if (!_inventory.TryEquip(terminator, pinpointer, "pinpointerpocket", force: true))
            QueueDel(pinpointer);

        args.Entity = terminator;
    }

    private void OnAfterAntagEntitySelected(Entity<TerminatorRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        var spawnPosition = Transform(args.EntityUid).Coordinates;

        Spawn(SpawnEffectPrototype, spawnPosition);
        _emp.EmpPulse(spawnPosition, EmpPower, 5000f, EmpPower * TimeSpan.FromSeconds(2));
    }

    private Entity<MindComponent>? FindValidPlayer()
    {
        var validPlayers = _mind.GetAliveHumans().Where(mind => !HasComp<NoObjectiveTargetComponent>(mind.Comp.OwnedEntity)).ToHashSet();
        if (validPlayers.Count == 0) return null;
        return _random.Pick(validPlayers);
    }
}
