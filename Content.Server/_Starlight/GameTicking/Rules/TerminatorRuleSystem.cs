using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Objectives.Components;
using Content.Shared._Starlight.Terminator;
using Content.Shared.Inventory;
using Content.Shared.Pinpointer;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed partial class TerminatorRuleSystem : GameRuleSystem<TerminatorRuleComponent>
{
    [Dependency] private readonly SharedPinpointerSystem _pinpointer = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private EntProtoId TerminatorEntityPrototype = "MobHumanTerminator";
    private EntProtoId PinpointerPrototype = "PinpointerTerminator";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerminatorRuleComponent, AntagSelectEntityEvent>(OnAntagSelectEntity);
    }

    private void OnAntagSelectEntity(Entity<TerminatorRuleComponent> ent, ref AntagSelectEntityEvent args)
    {
        if (args.Session?.AttachedEntity is not { } spawner) return;

        if (ent.Comp.Target == null)
        {
            return; // todo random player for generic gamemode
        }

        var terminator = Spawn(TerminatorEntityPrototype);
        var targetOverride = EnsureComp<TargetOverrideComponent>(terminator);
        targetOverride.Target = ent.Comp.Target;
        var termComp = EnsureComp<TerminatorComponent>(terminator);
        termComp.TargetBody = ent.Comp.TargetBody;

        // give the terminator a pinpointer that is pointing toward the target
        var pinpointer = Spawn(PinpointerPrototype);
        _pinpointer.SetTarget(pinpointer, ent.Comp.TargetBody);
        _pinpointer.SetActive(pinpointer, true);
        if (!_inventory.TryEquip(terminator, pinpointer, "pinpointerpocket", force: true))
            QueueDel(pinpointer);

        args.Entity = terminator;
    }
}