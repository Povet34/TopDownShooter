# EscapeFromDesertPlanet
EscapeFromDesertPlanet

<img width="1106" height="617" alt="image" src="https://github.com/user-attachments/assets/832ed8c7-141b-4291-97f3-724be83a8e7c" />



<img width="1899" height="1057" alt="image" src="https://github.com/user-attachments/assets/c223a883-c3db-46be-8a25-2d95f667ffd3" />



- Car
- FOV (FOW)
- DATA

## Reusable packages (`packages/`)

The pure gameplay logic (`TDS.Core`) is split into **12 independent Unity packages** —
each installable on its own with **zero cross-package dependencies** and its EditMode
tests included. Install via **Package Manager → Add package from git URL**, swapping the
folder name and `<ref>` (a branch or tag; currently `feature/inventory-ui-and-car-entry`):

```
https://github.com/Povet34/EscapeFromDesertPlanet.git?path=/packages/<folder>#<ref>
```

| Folder (`packages/…`) | Use |
|---|---|
| `com.povet34.grid-inventory` | Multi-cell grid inventory (Tarkov/Diablo): place / auto-place / rotate / remove |
| `com.povet34.tds-loot` | Loot wallet + drop chance/amount rolls |
| `com.povet34.tds-extraction` | Extraction board/stay progress + win/lose outcome |
| `com.povet34.tds-minimap` | World → minimap projection with edge clamping |
| `com.povet34.tds-spawning` | Spawn tables, weighted selection, monster defs, wave sequencing |
| `com.povet34.tds-ai` | Cover eval/approach, perception FSM, ranged engage, evasion, view cone, strafe, stuck, aim, noise… |
| `com.povet34.tds-squad` | Squad formation (spiral), decisions, roaming |
| `com.povet34.tds-camera` | Camera follow, shake, zoom, hit-stop |
| `com.povet34.tds-vehicle` | Vehicle exit-point selection |
| `com.povet34.tds-services` | Service registry/locator, boot sequence, spawn point, service interfaces |
| `com.povet34.tds-combat-fx` | Breakable health, explosion model, low-health vignette |
| `com.povet34.dev-console` | In-game dev console command registry/parser (`cmd arg1 arg2` → dispatch) |

Example — grid inventory only:

```
https://github.com/Povet34/EscapeFromDesertPlanet.git?path=/packages/com.povet34.grid-inventory#feature/inventory-ui-and-car-entry
```

See [`packages/README.md`](packages/README.md) for details. Types live in the `TDS.Core`
namespace; pure C# (only `UnityEngine`/`System`).
