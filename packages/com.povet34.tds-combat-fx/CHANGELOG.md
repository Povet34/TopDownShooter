# Changelog

## [0.2.0]
- `StatusEffects`: character debuffs — bleed (DoT/sec), slow (speed cut) and stun,
  with refresh-on-reapply, `Tick(dt)` returning accumulated bleed + expiring effects,
  and `SpeedMultiplier`/`IsStunned` aggregation for movement glue.

## [0.1.0]
- Initial extraction from EscapeFromDesertPlanet TDS.Core.
