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

Run the AI-vs-AI balance smoke in Unity batchmode with the same environment required for tests: set `ALLUSERSPROFILE=C:\ProgramData`, `ProgramData=C:\ProgramData`, and `TMP=%TEMP%`, then call `Unity.exe -quit -batchmode -projectPath C:\dev\Tower -executeMethod Tower.EditorTools.SimRunner.RunDefault -logFile -`. The runner writes JSON to `C:\dev\_setup\sim-result.json` by default; override with `-simOutput <path>` and tune the sample with `-simSeed <int>`, `-simBattles <int>`, or `-simMaxRounds <int>`.
