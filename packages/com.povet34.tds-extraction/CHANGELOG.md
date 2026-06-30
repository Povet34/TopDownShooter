# Changelog

## [0.4.0]
- `StashUpgrades.Default`: + firepower (bullet damage), munitions (reserve ammo),
  insurance (death recovery) upgrade defs.
- `Insurance.Recovered(amount, rate)`: floor(amount × clamp01(rate)) for death-time
  partial extraction / insurance payout.

## [0.3.0]
- `MetaStash.TrySpend`: deduct currency only when affordable (for purchases).
- `StashUpgrades`: buyable permanent upgrades — code-fixed defs (vitality/swiftness/padding),
  linear cost scaling (BaseCost×(level+1)), level cap, `TotalBonus`, level serialization.

## [0.2.0]
- `MetaStash`: persistent run-to-run loot bank — accumulate currency + item counts,
  serialize/deserialize to a string (for PlayerPrefs etc.), tolerant of bad input.

## [0.1.0]
- Initial extraction from EscapeFromDesertPlanet TDS.Core.
