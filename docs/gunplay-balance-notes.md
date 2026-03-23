# Gunplay Balance Notes

This branch balances gunplay in two layers:

1. Shared spread logic
- Weapon spread is progressive. Shots raise a `TargetAngle`, and the current spread converges toward it over time.
- Movement spread is tracked separately from shot spread, so walking and sprinting push the weapon toward a worse ceiling without making the first shot jump instantly.
- Spread caps are state-based and progressive:
  - standing still reduces the spread ceiling the most
  - walking with `Shift` reduces it less
  - normal movement keeps the full weapon ceiling

2. Per-weapon YAML tuning
- Pistols: low spread ceiling, low increase, high decay.
- Revolvers: precise first shots, harsher spam punishment.
- Rifles: more controlled than SMGs, but sustained fire still opens up.
- SMGs: higher spread ceiling and stronger sustained bloom than rifles.
- DMRs: low ceiling, low baseline spread, rewarded burst/tap fire.
- Snipers: very low baseline spread, punished more by movement than by spam.
- LMGs: highest ceiling for sustained fire and suppression.

Current testing focus:
- compare `still` vs `Shift` vs `normal movement`
- compare `tap fire`, `short bursts`, and `full spray`
- validate at medium range, not only point-blank

Important implementation note:
- The shot spread now uses the full random range `[-1, 1]`. Earlier it effectively used half the tuned spread, which made recoil changes appear much smaller than intended.
