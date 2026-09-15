using Content.Server._Starlight.GameTicking.Rules;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [Dependency] private DynamicRuleCooldownSystem _dynamicRuleCooldown = default!;
}
