using System.Linq;
using Content.Server._Starlight.SecureTerminal;
using Content.Shared.GameTicking;
using Content.Shared._Starlight.SecureTerminal;

namespace Content.Server._Starlight.Statistics;

public sealed partial class RoundStatisticsSystem
{
    private readonly Dictionary<SecureTerminalKey, SecureTerminalAccumulator> _secureTerminalRequests = [];

    private void InitializeSecureTerminalStatistics()
        => RegisterStatisticsDomain(EmitSecureTerminalStatistics, ClearSecureTerminalStatistics);

    /// <summary>
    /// Records a proposal being opened, along with the fee held from the requester until it resolves.
    /// </summary>
    public void RecordSecureTerminalProposal(string requestId, SecureTerminalActionType action, bool reasonGiven, int feeHeld)
    {
        if (GetSecureTerminalRequest(requestId, action) is not { } statistic)
            return;

        statistic.Proposed++;
        statistic.FeeHeld += Math.Max(0, feeHeld);

        if (reasonGiven)
            statistic.Reasons++;
    }

    /// <summary>
    /// Records one authorization of a proposal. An admin console approval is counted apart from the
    /// command signatures the request demands of the crew.
    /// </summary>
    public void RecordSecureTerminalAuthorization(string requestId, SecureTerminalActionType action, bool admin)
    {
        if (GetSecureTerminalRequest(requestId, action) is not { } statistic)
            return;

        if (admin)
            statistic.AdminApprovals++;
        else
            statistic.Signatures++;
    }

    /// <summary>
    /// Records a proposal collecting every signature it needed and starting its countdown, after
    /// spending <paramref name="pending"/> waiting for them.
    /// </summary>
    public void RecordSecureTerminalActivation(string requestId, SecureTerminalActionType action, TimeSpan pending, float salaryPenalty)
    {
        if (GetSecureTerminalRequest(requestId, action) is not { } statistic)
            return;

        statistic.Authorized++;
        statistic.PendingSeconds += Math.Max(0d, pending.TotalSeconds);
        statistic.SalaryPenalty += salaryPenalty;
    }

    /// <summary>
    /// Records how a proposal stopped being pending. An armory that arrives and is later sent back
    /// counts as both executed and recalled.
    /// </summary>
    public void RecordSecureTerminalOutcome(string requestId, SecureTerminalActionType action, SecureTerminalResult result)
    {
        if (GetSecureTerminalRequest(requestId, action) is not { } statistic)
            return;

        switch (result)
        {
            case SecureTerminalResult.Executed:
                statistic.Executed++;
                break;
            case SecureTerminalResult.Denied:
                statistic.Denied++;
                break;
            case SecureTerminalResult.Expired:
                statistic.Expired++;
                break;
            case SecureTerminalResult.Recalled:
                statistic.Recalled++;
                break;
        }
    }

    /// <summary>
    /// Records credits handed back to a requester whose proposal did not go through.
    /// </summary>
    public void RecordSecureTerminalRefund(string requestId, SecureTerminalActionType action, int amount)
    {
        if (GetSecureTerminalRequest(requestId, action) is not { } statistic)
            return;

        statistic.FeeRefunded += Math.Max(0, amount);
    }

    private void EmitSecureTerminalStatistics(RoundEndMessageEvent args)
    {
        var proposed = 0;
        var authorized = 0;
        var executed = 0;
        var denied = 0;
        var expired = 0;
        var recalled = 0;
        var signatures = 0;
        var feeHeld = 0;
        var feeRefunded = 0;

        foreach (var (key, statistic) in _secureTerminalRequests)
        {
            proposed += statistic.Proposed;
            authorized += statistic.Authorized;
            executed += statistic.Executed;
            denied += statistic.Denied;
            expired += statistic.Expired;
            recalled += statistic.Recalled;
            signatures += statistic.Signatures;
            feeHeld += statistic.FeeHeld;
            feeRefunded += statistic.FeeRefunded;

            EmitRoundRecord(
                args.RoundId,
                "kind=secure_terminal request_id={RequestId} action_type={ActionType} proposed={Proposed} " +
                "reasons={Reasons} signatures={Signatures} admin_approvals={AdminApprovals} " +
                "authorized={Authorized} executed={Executed} denied={Denied} expired={Expired} " +
                "recalled={Recalled} pending_seconds_total={PendingSecondsTotal} fee_held={FeeHeld} " +
                "fee_refunded={FeeRefunded} salary_penalty_total={SalaryPenaltyTotal}",
                key.Request,
                key.Action,
                statistic.Proposed,
                statistic.Reasons,
                statistic.Signatures,
                statistic.AdminApprovals,
                statistic.Authorized,
                statistic.Executed,
                statistic.Denied,
                statistic.Expired,
                statistic.Recalled,
                Number(statistic.PendingSeconds),
                statistic.FeeHeld,
                statistic.FeeRefunded,
                Number(statistic.SalaryPenalty));
        }

        EmitRoundRecord(
            args.RoundId,
            "kind=secure_terminal_summary requests={Requests} proposed={Proposed} authorized={Authorized} " +
            "executed={Executed} denied={Denied} expired={Expired} recalled={Recalled} signatures={Signatures} " +
            "fee_held={FeeHeld} fee_refunded={FeeRefunded} salary_penalty={SalaryPenalty}",
            _secureTerminalRequests.Count,
            proposed,
            authorized,
            executed,
            denied,
            expired,
            recalled,
            signatures,
            feeHeld,
            feeRefunded,
            Number(SampleSalaryPenalty()));
    }

    /// <summary>
    /// The penalty stacks across requests and is capped per station, so the worst station's final
    /// value is what the crew was actually left paying.
    /// </summary>
    private float SampleSalaryPenalty()
    {
        var penalty = 0f;

        var query = EntityQueryEnumerator<SecureCommandTerminalStationComponent>();
        while (query.MoveNext(out _, out var station))
        {
            var stationPenalty = station.SalaryModifiers.Values
                .Where(modifier => modifier < 0)
                .Sum(modifier => -modifier);
            penalty = Math.Max(penalty, stationPenalty);
        }

        return penalty;
    }

    private void ClearSecureTerminalStatistics()
        => _secureTerminalRequests.Clear();

    private SecureTerminalAccumulator? GetSecureTerminalRequest(string requestId, SecureTerminalActionType action)
    {
        if (!EnsureRound())
            return null;

        var key = new SecureTerminalKey(requestId, action);
        if (_secureTerminalRequests.TryGetValue(key, out var statistic))
            return statistic;

        statistic = new SecureTerminalAccumulator();
        _secureTerminalRequests.Add(key, statistic);
        return statistic;
    }

    private sealed class SecureTerminalAccumulator
    {
        public int Proposed;
        public int Reasons;
        public int Signatures;
        public int AdminApprovals;
        public int Authorized;
        public int Executed;
        public int Denied;
        public int Expired;
        public int Recalled;
        public int FeeHeld;
        public int FeeRefunded;
        public double PendingSeconds;
        public float SalaryPenalty;
    }

    private readonly record struct SecureTerminalKey(string Request, SecureTerminalActionType Action);
}

/// <summary>
/// How a secure command terminal proposal stopped being pending.
/// </summary>
public enum SecureTerminalResult
{
    Executed,
    Denied,
    Expired,
    Recalled,
}
