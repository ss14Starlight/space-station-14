using System.Linq;
using Content.Server._Starlight.Commands;
using Content.Server.Administration;
using Content.Shared._Starlight.Devil;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Devil;

[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class DevilCommand : ToolshedCommand
{
    [CommandImplementation("querysouls")]
    public EntityUid QuerySouls(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        var ev = new DevilSoulsDamnedCountChangedEvent();
        EntityManager.EventBus.RaiseLocalEvent(uid, ref ev);
        return uid;
    }

    [CommandImplementation("setreq")]
    public EntityUid SetRequirement(IInvocationContext ctx, [PipedArgument] EntityUid uid, DevilChangeRequirementType type, int value)
    {
        if (!TryComp<DevilComponent>(uid, out var devil))
        {
            CommandMarkup.Error(ctx, $"Entity {EntityManager.ToPrettyString(uid)} is not a devil.");
            return uid;
        }

        switch (type)
        {
            case DevilChangeRequirementType.RedEyes:
                devil.RedEyesAppearance.AtSouls = value;
                break;
            case DevilChangeRequirementType.EvilHalo:
                devil.EvilHaloAppearance.AtSouls = value;
                break;
            case DevilChangeRequirementType.OminousHum:
                devil.OminousHum.AtSouls = value;
                break;
            case DevilChangeRequirementType.RedAura:
                devil.RedAuraAppearance.AtSouls = value;
                break;
            case DevilChangeRequirementType.Bident:
                devil.BidentAction.AtSouls = value;
                break;
            case DevilChangeRequirementType.InfernalJaunt:
                devil.InfernalJauntAction.AtSouls = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        EntityManager.Dirty(uid, devil);
        ctx.WriteLine($"Updated {type} requirement for {EntityManager.ToPrettyString(uid)}.");
        return uid;
    }

    [CommandImplementation("querysouls")]
    public IEnumerable<EntityUid> QuerySouls(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => QuerySouls(ctx, x));

    [CommandImplementation("setreq")]
    public IEnumerable<EntityUid> SetRequirement(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        DevilChangeRequirementType type, int value) =>
        uid.Select(x => SetRequirement(ctx, x, type, value));
}

// I don't feel like writing another parser right now or rewriting devil system, so you get enum that you must update if new reqs are added. :)
public enum DevilChangeRequirementType : byte
{
    RedEyes,
    EvilHalo,
    OminousHum,
    RedAura,
    Bident,
    InfernalJaunt,
}
