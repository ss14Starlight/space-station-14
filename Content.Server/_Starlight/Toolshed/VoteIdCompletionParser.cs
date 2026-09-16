using System.Linq;
using Content.Server.Voting.Managers;
using Robust.Shared.Console;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server._Starlight.Toolshed;

public sealed partial class VoteIdCompletionParser : CustomCompletionParser<int>
{
    [Dependency] private IVoteManager _vote = default!;

    public override CompletionResult? TryAutocomplete(ParserContext ctx, CommandArgument? arg)
    {
        var options = _vote.ActiveVotes.OrderBy(v => v.Id)
            .Select(v => new CompletionOption(v.Id.ToString(), $"{v.Title} ({v.InitiatorText})")).ToList();
        return options.Count == 0
            ? new CompletionResult([], "[There are no active votes]")
            : CompletionResult.FromHintOptions(options, "Vote ID");
    }
}
