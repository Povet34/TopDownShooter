# Dev Console (`com.povet34.dev-console`)

Pure command registry/parser for an in-game developer console. Register named commands,
parse `cmd arg1 arg2`, dispatch, and get an output string back. Bring your own UI.

```csharp
var reg = new TDS.Core.ConsoleRegistry();
reg.Register("tp", "tp <x> <z>", args => { /* move player */ return "teleported"; });
reg.Register("help", "list commands", _ => string.Join("\n", ... ));

string output = reg.Execute("tp 10 20"); // "teleported"
reg.Execute("nope");                      // "unknown command: nope  (type 'help')"
```

- Case-insensitive command names, last registration wins.
- Whitespace-split args, empty input is a no-op, handler exceptions are caught and
  returned as `error: ...`.
- Pure C# (no `UnityEngine`), EditMode-tested.

The UI (backtick toggle, input field, output log) and the game-specific commands
(spawn, teleport, heal…) are glue on top — see `DevConsole` in the consuming project.
