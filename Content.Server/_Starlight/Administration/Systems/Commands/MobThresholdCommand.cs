using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Administration.Systems.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Fun)]
public sealed class MobThresholdCommand : ToolshedCommand
{
    private MobThresholdSystem? _threshold;

    [CommandImplementation("initialize")]
    public EntityUid Initialize([PipedArgument] EntityUid uid, FixedPoint2 damage, MobState state)
    {
        _threshold ??= GetSys<MobThresholdSystem>();
        var comp = EnsureComp<MobThresholdsComponent>(uid);
        EnsureComp<MobStateComponent>(uid);
        _threshold.SetMobStateThreshold(uid, damage, state, comp);
        return uid;
    }

    [CommandImplementation("initialize")]
    public IEnumerable<EntityUid> Initialize([PipedArgument] IEnumerable<EntityUid> uid, FixedPoint2 damage,
        MobState state) => uid.Select(x => Initialize(x, damage, state));
}
