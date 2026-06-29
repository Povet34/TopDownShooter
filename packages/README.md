# TDS reusable packages

Domain-scoped, pure-C# gameplay packages extracted from this project's `TDS.Core`
assembly. Each is an independent UPM package — **zero cross-package dependencies**, only
`UnityEngine`/`System` (one uses `UnityEngine.AI`). Types live in the `TDS.Core` namespace.
Each ships its EditMode tests under `Tests/Editor`.

> Verification: split was statically verified (every test references only its own
> package's types; all 53 source files assigned, 0 cross-package violations). The code is
> identical to the project's `TDS.Core`, which passes 259 EditMode tests. Import into a
> fresh project + run the Test Runner to confirm in your Unity version.

## Install (Package Manager → Add package from git URL)

Replace `<ref>` with a branch or tag (e.g. `main`, or a release tag once you cut one;
currently on branch `feature/inventory-ui-and-car-entry`).

| Package | What |
|---|---|
| `com.povet34.grid-inventory` | Multi-cell grid inventory (Tarkov/Diablo) |
| `com.povet34.tds-loot` | Loot wallet + drop rolls |
| `com.povet34.tds-extraction` | Extraction progress + win/lose outcome |
| `com.povet34.tds-minimap` | World→minimap projection |
| `com.povet34.tds-spawning` | Spawn tables / selection / waves |
| `com.povet34.tds-ai` | Cover, perception FSM, engage, evasion, view cone, … |
| `com.povet34.tds-squad` | Squad formation / decision / roam |
| `com.povet34.tds-camera` | Follow, shake, zoom, hit-stop |
| `com.povet34.tds-vehicle` | Vehicle exit-point pick |
| `com.povet34.tds-services` | Service registry, boot, spawn point |
| `com.povet34.tds-combat-fx` | Breakable health, explosion, vignette |

```
https://github.com/Povet34/EscapeFromDesertPlanet.git?path=/packages/com.povet34.grid-inventory#<ref>
```

(Same URL pattern for each — swap the package folder name.)

## Notes
- These are **snapshot copies** of `TDS.Core`; the game project still uses its own copy.
  To make a package its own repository later, move its folder out and `git init` there
  (or use a monorepo split tool). The `?path=` form above already works as-is.
- Add a `LICENSE` before distributing publicly.
