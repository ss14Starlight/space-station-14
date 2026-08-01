using Content.Shared.GameTicking;
using Content.Shared.Radio;

namespace Content.Server.Radio.EntitySystems;

public sealed partial class RadioSystem
{
    [Dependency] private SharedGameTicker _ticker = default!;

    private string ObfuscateName(string anonymousAlias, EntityUid source)
    {
        int hash = HashCode.Combine(source, _ticker.RoundId); // Unique value per character per round
        hash = hash & 0x7FFFFFFF; // Clear sign bit to ensure non-negative
        hash = hash % 900 + 100; // result is a number 100-999
        return string.Format(anonymousAlias, hash);
    }
}
