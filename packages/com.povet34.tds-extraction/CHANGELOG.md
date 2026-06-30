# Changelog

## [0.3.0]
- `MetaStash.TrySpend`: deduct currency only when affordable (for purchases).
- `StashUpgrades`: buyable permanent upgrades — code-fixed defs (vitality/swiftness/padding),
  linear cost scaling (BaseCost×(level+1)), level cap, `TotalBonus`, level serialization.

## [0.2.0]
- `MetaStash`: persistent run-to-run loot bank — accumulate currency + item counts,
  serialize/deserialize to a string (for PlayerPrefs etc.), tolerant of bad input.

## [0.1.0]
- Initial extraction from EscapeFromDesertPlanet TDS.Core.
