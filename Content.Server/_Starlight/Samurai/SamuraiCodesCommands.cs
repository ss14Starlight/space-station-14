using System.Linq;
using Content.Server.Administration;
using Content.Shared._Starlight.Samurai;
using Content.Shared.Administration;
using Content.Shared.Dataset;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Samurai;

[AdminCommand(AdminFlags.Admin)]
internal sealed partial class SamuraiSharedCodesCommand : LocalizedCommands
{
    [Dependency] private IEntitySystemManager _entman = default!;
    private SamuraiCodesSystem? _codes;
    public override string Command => "samuraishared";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _codes ??= _entman.GetEntitySystem<SamuraiCodesSystem>();
        var codes = _codes.SharedCodes;
        foreach (var code in codes)
        {
            shell.WriteLine($"{code.GetLocName()}: {code.GetLocDesc()}");
        }
    }
}

[AdminCommand(AdminFlags.Admin)]
internal sealed partial class SamuraiRerollCodesCommand : LocalizedCommands
{
    [Dependency] private IEntitySystemManager _entman = default!;
    private SamuraiCodesSystem? _codes;
    public override string Command => "samurairerollshared";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _codes ??= _entman.GetEntitySystem<SamuraiCodesSystem>();
        _codes.NewSharedCodes();
    }
}

[ToolshedCommand(Name = "codes"), AdminCommand(AdminFlags.Admin)]
public sealed class AdminCodesCommand : ToolshedCommand
{
    private SamuraiCodesSystem? _codes;

    [CommandImplementation("addproto")]
    public EntityUid AddCodeProto(
        [PipedArgument] EntityUid input,
        [CommandArgument] ProtoId<SamuraiCodePrototype> code
    )
    {
        _codes ??= GetSys<SamuraiCodesSystem>();

        if (TryComp<SamuraiCodesComponent>(input, out var codesComponent))
        {
            _codes.TryAddCode((input, codesComponent), code);
        }

        return input;
    }

    [CommandImplementation("addproto")]
    public IEnumerable<EntityUid> AddCodeProto(
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] ProtoId<SamuraiCodePrototype> language
    ) => input.Select(x => AddCodeProto(x, language));

    [CommandImplementation("adddataset")]
    public EntityUid AddCodeDataset(
        [PipedArgument] EntityUid input,
        [CommandArgument] ProtoId<DatasetPrototype> dataset
    )
    {
        _codes ??= GetSys<SamuraiCodesSystem>();

        if (TryComp<SamuraiCodesComponent>(input, out var codesComponent))
        {
            var ent = (input, codesComponent);
            if (_codes.TryPick(dataset, out var proto, _codes.GetActiveCodes(ent)))
                _codes.TryAddCode(ent, proto, true, false);
        }

        return input;
    }

    [CommandImplementation("adddataset")]
    public IEnumerable<EntityUid> AddCodeDataset(
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] ProtoId<DatasetPrototype> dataset
    ) => input.Select(x => AddCodeDataset(x, dataset));

    [CommandImplementation("addraw")]
    public EntityUid AddCodeRaw(
        [PipedArgument] EntityUid input,
        [CommandArgument] string title,
        [CommandArgument] string description
    )
    {
        _codes ??= GetSys<SamuraiCodesSystem>();

        if (TryComp<SamuraiCodesComponent>(input, out var codesComponent))
        {
            var ent = (input, codesComponent);
            var code = new SamuraiCode
            {
                CodeName = title,
                CodeDesc = description
            };
            _codes.AddCode(ent, code);
        }

        return input;
    }

    [CommandImplementation("addraw")]
    public IEnumerable<EntityUid> AddCodeDataset(
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] string title,
        [CommandArgument] string description
    ) => input.Select(x => AddCodeRaw(x, title, description));

    [CommandImplementation("ensure")]
    public EntityUid EnsureCode(
        [PipedArgument] EntityUid input
    )
    {
        _codes ??= GetSys<SamuraiCodesSystem>();

        if (!EntityManager.EnsureComponent<SamuraiCodesComponent>(input, out var codes))
            _codes.NotifyCodeChange((input, codes));

        return input;
    }

    [CommandImplementation("ensure")]
    public IEnumerable<EntityUid> EnsureCode(
        [PipedArgument] IEnumerable<EntityUid> input
    ) => input.Select(x => EnsureCode(x));

    [CommandImplementation("rm")]
    public EntityUid RemoveCode(
    [PipedArgument] EntityUid input,

    [CommandArgument] int index = 0
    )
    {
        _codes ??= GetSys<SamuraiCodesSystem>();

        if (TryComp<SamuraiCodesComponent>(input, out var codes))
            _codes.RemoveCode((input, codes), index);

        return input;
    }

    [CommandImplementation("rm")]
    public IEnumerable<EntityUid> RemoveCode(
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] int index = 0
    ) => input.Select(x => RemoveCode(x, index));
}
