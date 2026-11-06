using Content.Server.GameTicking;
using Content.Shared.GameTicking;

namespace Content.Server._Starlight.Statistics;

public sealed partial class RoundStatisticsSystem
{
    /// <summary>
    /// Emits a completed vote immediately so votes held after the round summary are not discarded.
    /// <c>for_next_round</c> says whether <c>round_id</c> is also the round the vote decides.
    /// </summary>
    public void RecordVoteResult(string voteType, string optionId, int votes, bool winner)
    {
        var forNextRound = _gameTicker.RunLevel != GameRunLevel.PreRoundLobby;

        EmitCurrentRoundRecord(
            "kind=vote_result vote_type={VoteType} option_id={OptionId} votes={Votes} winner={Winner} " +
            "for_next_round={ForNextRound}",
            voteType,
            optionId,
            votes,
            winner,
            forNextRound);
    }
}
