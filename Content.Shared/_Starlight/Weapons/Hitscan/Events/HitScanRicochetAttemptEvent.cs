using System.Numerics;

namespace Content.Shared._Starlight.Weapons.Hitscan.Events;

[ByRefEvent]
public record struct HitScanRicochetAttemptEvent(float Chance, Vector2 Pos, Vector2 Dir, bool Ricocheted)
{
}
