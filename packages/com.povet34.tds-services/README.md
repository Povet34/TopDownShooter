# TDS Boot & Services

Lightweight service registry/locator, boot sequencing, systems ensure, scene entry, player spawn point/spawner and service interfaces. Pure C#, EditMode-tested.

Pure C# (only UnityEngine), no external package dependencies. Types live in the `TDS.Core` namespace.

## Install (Unity Package Manager → Add from git URL)

```
https://github.com/Povet34/EscapeFromDesertPlanet.git?path=/packages/com.povet34.tds-services#<branch-or-tag>
```

## Runtime
- `GameBootstrap`
- `GameServices`
- `ServiceRegistry`
- `SystemsEnsurer`
- `BootSequence`
- `SceneEntryPoint`
- `PlayerSpawner`
- `PlayerSpawnPoint`
- `IClockService`
- `IControlsService`
- `IGameStateService`
- `IMissionService`
- `IObjectPoolService`
- `ICombatFeedbackService`

## Tests
EditMode tests ship under `Tests/Editor` (5 file(s)).

## License
Add a LICENSE file before distributing.
