using System.Linq;
using Content.Server._Starlight.Toolshed;
using Content.Server.GameTicking;
using Content.Shared.Roles.Components;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server._Starlight.Roles;

public sealed partial class RoleProtoIdTypeParser : TypeParser<MindRoleProtoId>
{
    [Dependency] private IPrototypeManager _proto = default!;

    public override bool TryParse(ParserContext ctx, out MindRoleProtoId result) =>
        EntProtoIdCompTypeParser<MindRoleProtoId, MindRoleComponent>.TryParse(Toolshed, _proto, ctx, out result);

    public override CompletionResult? TryAutocomplete(ParserContext ctx, CommandArgument? arg) =>
        EntProtoIdCompTypeParser<MindRoleProtoId, MindRoleComponent>.TryAutocomplete(_proto, ctx, arg);
}

public sealed partial class MindRoleEntityTypeParser : TypeParser<MindRoleEntity>
{
    [Dependency] private IEntityManager _entMan = default!;

    public override bool TryParse(ParserContext ctx, out MindRoleEntity result) =>
        SLEntityTypeParser<MindRoleEntity, MindRoleComponent>.TryParse(_entMan, ctx, out result);

    public override CompletionResult? TryAutocomplete(ParserContext ctx, CommandArgument? arg) =>
        SLEntityTypeParser<MindRoleEntity, MindRoleComponent>.TryAutocomplete(_entMan, ctx, arg);
}

public readonly record struct MindRoleProtoId(EntProtoId ProtoId) : IEntProtoIdCompStructWrapper
{
    public static object Create(EntProtoId protoId) => new MindRoleProtoId(protoId);
}

public readonly record struct MindRoleEntity(Entity<MindRoleComponent> Entity)
    : IEntityStructWrapper<MindRoleComponent>
{
    public static object Create(Entity<MindRoleComponent> entity) => new MindRoleEntity(entity);
}
