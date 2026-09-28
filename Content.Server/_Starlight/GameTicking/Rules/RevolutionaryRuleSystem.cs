//ReSharper disable CheckNamespace

using Content.Shared.Revolutionary.Components;

namespace Content.Server.GameTicking.Rules;

public sealed partial class RevolutionaryRuleSystem
{
    /// <summary>
    /// Blocks re-conversion of already-converted targets. Needed separately from the
    /// blacklist below since that's skipped for AlwaysRevolutionaryConvertible mobs (e.g. borgis).
    /// </summary>
    private bool IsAlreadyRevolutionary(EntityUid target) => HasComp<RevolutionaryComponent>(target) || HasComp<HeadRevolutionaryComponent>(target);
}
