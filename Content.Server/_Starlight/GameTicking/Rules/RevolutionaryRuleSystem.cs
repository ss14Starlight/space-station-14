//ReSharper disable CheckNamespace

using Content.Shared.Revolutionary.Components;

namespace Content.Server.GameTicking.Rules;

public sealed partial class RevolutionaryRuleSystem
{
    /// <summary>
    /// Blocks re-conversion of already-converted targets.
    /// </summary>
    private bool IsAlreadyRevolutionary(EntityUid target) => HasComp<RevolutionaryComponent>(target) || HasComp<HeadRevolutionaryComponent>(target);
}
