# Tower Coding Conventions

This document defines the baseline conventions for Tower implementation work.

## C# Style

- Use one top-level class per `.cs` file.
- Match the file name to the class name.
- Use namespaces under `Tower.*`.
- Use `_camelCase` for private fields.
- Prefer explicit, small types over broad utility classes.
- Keep comments short and reserve them for non-obvious behavior.

## Assembly Boundaries

- `Tower.Core`: data and rules first. Keep engine coupling minimal and prefer plain C# where possible.
- `Tower.Combat`: combat runtime logic. May reference `Tower.Core`.
- `Tower.Gen`: procedural generation logic. May reference `Tower.Core`.
- `Tower.UI`: player-facing presentation and interaction. May reference `Tower.Core` and `Tower.Combat`.
- `Tower.Tests.EditMode`: EditMode tests for the Tower assemblies.

## Core Logic

Core game rules should live outside `MonoBehaviour` classes unless Unity lifecycle behavior is the point of the type. Write combat rules, initiative rules, save rules, and data transformations so they can be exercised by EditMode unit tests.

## Data-Driven Content

Characters, passives, abilities, marks, and tags should be extended by adding data assets, not by growing switch statements. Prefer `ScriptableObject` definitions for content that designers or future agents need to add safely.

## Tests

- Add focused EditMode tests for rule-heavy work.
- T3 turn-engine work and T4 ability-pipeline work must include unit tests before merge.

## Combat Simulation

Run the AI-vs-AI balance smoke in Unity batchmode with the same environment required for tests: set `ALLUSERSPROFILE=C:\ProgramData`, `ProgramData=C:\ProgramData`, and `TMP=%TEMP%`, then call `Unity.exe -quit -batchmode -projectPath C:\Users\fancy\Tower -executeMethod Tower.EditorTools.SimRunner.RunDefault -logFile -`. The runner writes JSON to `C:\dev\_setup\sim-result.json` by default; override with `-simOutput <path>` and tune the sample with `-simSeed <int>`, `-simBattles <int>`, or `-simMaxRounds <int>`.

## QA Harness & Camera Tuning (dev only)

Both features are inert unless their command line argument is present; without the argument the code paths never activate.

- **QA TCP harness**: launch the player with `-qaPort <n>` (e.g. `Tower.exe -qaPort 7777`). A localhost-only, line-oriented TCP endpoint accepts `press <buttonGameObjectName>`, `state` (one-line JSON snapshot: scene, combat round/initiative/unit HP-position-marks, expedition floor/room/retreat), `scene <name>`, and `quit`. Responses are `OK`, `ERR <reason>`, or the JSON line. Only explicitly registered buttons and state contributors are exposed (`Tower.Core.QaRegistry` via `QaRuntime`) — scene controllers register their own uGUI buttons by GameObject name; reflection-based scene scans are forbidden. Quick check from PowerShell:
  `$c = New-Object Net.Sockets.TcpClient('127.0.0.1', 7777); $w = New-Object IO.StreamWriter($c.GetStream()); $w.AutoFlush = $true; $r = New-Object IO.StreamReader($c.GetStream()); $w.WriteLine('state'); $r.ReadLine()`
- **Camera tuning mode**: launch with `-devcam`. Keys: `I`/`K` pitch, `+`/`-` distance, `[`/`]` FOV; current values are drawn top-left; `P` dumps them to `%TEMP%\tower-cam.json` so a tuned setup can be promoted into `Tower.Core.CameraTuning`. v0 defaults live in `CameraTuning` (pitch 52, zoom range 8-20 m default 14, FOV 38, follow damping 0.12 s). Mouse scroll zoom is always active, in dev and non-dev builds alike.
