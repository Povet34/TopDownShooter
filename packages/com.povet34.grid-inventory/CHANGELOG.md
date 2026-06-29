# Changelog

## [0.2.0]
- Free placement: `InventoryGrid.CanPlaceIgnoring` (ignore one/two items),
  `TryMove` (relocate, may overlap its own old cells) and `TrySwap` (exchange two
  items' positions, each keeping its rotation). All check before mutating, so a
  failed op leaves the grid (and item instances) unchanged.

## [0.1.0]
- Initial extraction from EscapeFromDesertPlanet TDS.Core.
