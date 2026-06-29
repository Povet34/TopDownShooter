# TDS Enemy AI

Top-down enemy AI sims: cover evaluation/approach, perception FSM, ranged-engage decision, evasion, view cone, strafe, stuck tracking, battle mover, aiming, moving spread, locomotion, noise model, map-object roles. Pure C#, EditMode-tested.

Pure C# (only UnityEngine), no external package dependencies. Types live in the `TDS.Core` namespace.

## Install (Unity Package Manager → Add from git URL)

```
https://github.com/Povet34/EscapeFromDesertPlanet.git?path=/packages/com.povet34.tds-ai#<branch-or-tag>
```

## Runtime
- `CoverEvaluation`
- `CoverApproach`
- `PerceptionFsm`
- `RangedEngageDecision`
- `EvasionPlanner`
- `ViewCone`
- `StrafeBlend`
- `StuckTracker`
- `BattleMover`
- `AimDirection`
- `AimRotation`
- `MovingSpread`
- `LocomotionAnim`
- `NoiseModel`
- `MapObjectRole`

## Tests
EditMode tests ship under `Tests/Editor` (15 file(s)).

## License
Add a LICENSE file before distributing.
