using System.Linq;
using Content.Server._Starlight.Commands;
using Robust.Shared.Console;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server._Starlight.Toolshed;

public sealed partial class ViewVariablesPathCompletionParser : CustomCompletionParser<string>
{
    [Dependency] private IEntityManager _ent = null!;
    [Dependency] private IComponentFactory _factory = null!;
    [Dependency] private IViewVariablesManager _vvm = null!;

    // Caps how many piped entities get listed, so piping something like `entities` doesn't hang completions.
    private const int MaxEvaluatedEntities = 32;

    public override CompletionResult TryAutocomplete(ParserContext ctx, CommandArgument? arg)
    {
        ctx.ConsumeWhitespace();

        var path = ctx.Input[ctx.Index..].Trim('"');
        var paths = IsEntityPiped(ctx.Bundle.PipedType)
            ? ListEntityRelativePath(ctx, path)
            : _vvm.ListPath(path, new VVListPathOptions());
        var opts = paths.Select(p => new CompletionOption($"\"{p}", null,
            CompletionOptionFlags.PartialCompletion | CompletionOptionFlags.NoEscape));

        return CompletionResult.FromHintOptions(opts, GetArgHint(arg));
    }

    private static bool IsEntityPiped(Type? pipedType)
        => pipedType is not null
            && (pipedType == typeof(EntityUid) || pipedType.IsAssignableTo(typeof(IEnumerable<EntityUid>)));

    private IEnumerable<string> ListEntityRelativePath(ParserContext ctx, string path)
    {
        var entities = CommandHelpers.EvaluatePipedInput(ctx) switch
        {
            EntityUid uid => [uid],
            IEnumerable<EntityUid> uids => uids.Take(MaxEvaluatedEntities).ToList(),
            _ => null,
        };

        if (entities is { Count: > 0 })
            return entities.SelectMany(uid => ListRelativeTo(uid, path)).Distinct();

        // Couldn't evaluate the piped input, so guess from any entity that has the component.
        var segments = path.TrimStart('/').Split('/');

        if (segments.Length <= 1)
            return _factory.AllRegisteredTypes.Select(t => $"/{_factory.GetComponentName(t)}");

        if (!_factory.TryGetRegistration(segments[0], out var registration, true))
            return [];

        var (example, _) = _ent.GetAllComponents(registration.Type, true).FirstOrDefault();
        return example.IsValid() ? ListRelativeTo(example, path) : [];
    }

    private IEnumerable<string> ListRelativeTo(EntityUid uid, string path)
    {
        var prefix = $"/entity/{uid}";
        return _vvm.ListPath($"{prefix}/{path.TrimStart('/')}", new VVListPathOptions())
            .Where(p => p.StartsWith(prefix))
            .Select(p => p[prefix.Length..]);
    }
}
