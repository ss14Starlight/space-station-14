using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared._Starlight.Commands;
using Content.Shared.Administration;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Damage;

// TODO: Support setting source uid when nullable parsing is added
[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed partial class DamageCommand : ToolshedCommand
{
    [Dependency] private IAdminLogManager _log = null!;
    [Dependency] private IPrototypeManager _proto = null!;
    [Dependency] private ISerializationManager _ser = null!;
    private DamageableSystem? _damage;

    [CommandImplementation("type")]
    public EntityUid DamageByType(IInvocationContext ctx, [PipedArgument] EntityUid uid,
        ProtoId<DamageTypePrototype> type, float quantity, [Optional] [DefaultParameterValue(true)] bool canHeal,
        [Optional] [DefaultParameterValue(0f)] float armorPenetration,
        [Optional] [DefaultParameterValue(false)] bool ignoreResistances,
        [Optional] [DefaultParameterValue(false)] bool ignoreGlobalModifiers,
        [Optional] [DefaultParameterValue(true)] bool interruptDoAfters)
    {
        var damage = new DamageSpecifier { DamageDict = { [type] = quantity } };
        DoDamage(ctx, damage, uid, armorPenetration, ignoreResistances, ignoreGlobalModifiers, interruptDoAfters,
            canHeal);
        return uid;
    }

    [CommandImplementation("group")]
    public EntityUid DamageByGroup(IInvocationContext ctx, [PipedArgument] EntityUid uid,
        ProtoId<DamageGroupPrototype> group, float quantity, [Optional] [DefaultParameterValue(true)] bool canHeal,
        [Optional] [DefaultParameterValue(0f)] float armorPenetration,
        [Optional] [DefaultParameterValue(false)] bool ignoreResistances,
        [Optional] [DefaultParameterValue(false)] bool ignoreGlobalModifiers,
        [Optional] [DefaultParameterValue(true)] bool interruptDoAfters)
    {
        var damage = new DamageSpecifier { DamageGroupDict = { [group] = quantity } };
        DoDamage(ctx, damage, uid, armorPenetration, ignoreResistances, ignoreGlobalModifiers, interruptDoAfters,
            canHeal);
        return uid;
    }

    [CommandImplementation("specifier")]
    public EntityUid DamageBySpecifier(IInvocationContext ctx, [PipedArgument] EntityUid uid, string yaml,
        [Optional] [DefaultParameterValue(true)] bool canHeal,
        [Optional] [DefaultParameterValue(0f)] float armorPenetration,
        [Optional] [DefaultParameterValue(false)] bool ignoreResistances,
        [Optional] [DefaultParameterValue(false)] bool ignoreGlobalModifiers,
        [Optional] [DefaultParameterValue(true)] bool interruptDoAfters)
    {
        if (!TryParseDamageSpecifier(ctx, yaml, out var damage)) return uid;
        DoDamage(ctx, damage, uid, armorPenetration, ignoreResistances, ignoreGlobalModifiers, interruptDoAfters,
            canHeal);
        return uid;
    }

    [CommandImplementation("type")]
    public IEnumerable<EntityUid> DamageByType(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        ProtoId<DamageTypePrototype> type, float quantity, [Optional] [DefaultParameterValue(true)] bool canHeal,
        [Optional] [DefaultParameterValue(0f)] float armorPenetration,
        [Optional] [DefaultParameterValue(false)] bool ignoreResistances,
        [Optional] [DefaultParameterValue(false)] bool ignoreGlobalModifiers,
        [Optional] [DefaultParameterValue(true)] bool interruptDoAfters) =>
        uid.Select(x => DamageByType(ctx, x, type, quantity, canHeal, armorPenetration, ignoreResistances,
            ignoreGlobalModifiers, interruptDoAfters));

    [CommandImplementation("group")]
    public IEnumerable<EntityUid> DamageByGroup(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        ProtoId<DamageGroupPrototype> group, float quantity, [Optional] [DefaultParameterValue(true)] bool canHeal,
        [Optional] [DefaultParameterValue(0f)] float armorPenetration,
        [Optional] [DefaultParameterValue(false)] bool ignoreResistances,
        [Optional] [DefaultParameterValue(false)] bool ignoreGlobalModifiers,
        [Optional] [DefaultParameterValue(true)] bool interruptDoAfters) =>
        uid.Select(x => DamageByGroup(ctx, x, group, quantity, canHeal, armorPenetration, ignoreResistances,
            ignoreGlobalModifiers, interruptDoAfters));

    [CommandImplementation("specifier")]
    public IEnumerable<EntityUid> DamageBySpecifier(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uids,
        string yaml, [Optional] [DefaultParameterValue(true)] bool canHeal,
        [Optional] [DefaultParameterValue(0f)] float armorPenetration,
        [Optional] [DefaultParameterValue(false)] bool ignoreResistances,
        [Optional] [DefaultParameterValue(false)] bool ignoreGlobalModifiers,
        [Optional] [DefaultParameterValue(true)] bool interruptDoAfters)
    {
        if (!TryParseDamageSpecifier(ctx, yaml, out var damage)) return uids;
        var targets = uids.ToList();
        foreach (var uid in targets)
            DoDamage(ctx, damage, uid, armorPenetration, ignoreResistances, ignoreGlobalModifiers, interruptDoAfters,
                canHeal);
        return targets;
    }

    private void DoDamage(IInvocationContext ctx, DamageSpecifier damage, EntityUid uid, float armorPenetration,
        bool ignoreResistances, bool ignoreGlobalModifiers, bool interruptDoAfters, bool canHeal)
    {
        _damage ??= GetSys<DamageableSystem>();
        var received = _damage.ChangeDamage(uid, damage, ignoreResistances, interruptDoAfters, null,
            ignoreGlobalModifiers, armorPenetration, canHeal);

        _log.Add(LogType.AdminCommands, LogImpact.Medium,
            $"{ctx.Session:executor} dealt {string.Join(", ", received.DamageDict.Select(kvp => {
                var type = _proto.Index(kvp.Key);
                return $"{kvp.Value} {type.LocalizedName}";
            })):types} damage to {EntityManager.ToPrettyString(uid):target} for a total of {received.GetTotal():damage}");
    }

    private bool TryParseDamageSpecifier(IInvocationContext ctx, string yaml,
        [NotNullWhen(true)] out DamageSpecifier? damage)
    {
        damage = null;
        var document = DataNodeParser.ParseYamlStream(new StringReader(yaml)).First();
        var node = document.Root;
        var errors = _ser.ValidateNode<DamageSpecifier>(node).GetErrors().ToList();
        if (errors.Count > 0)
        {
            foreach (var error in errors)
                CommandMarkup.Error(ctx, $"{error.Node.Start}: {error.ErrorReason}");
            return false;
        }

        damage = _ser.Read<DamageSpecifier>(node, notNullableOverride: true);
        return true;
    }
}
