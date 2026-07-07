# Tower static-data pipeline (build-time)

Excel (authoring) -> validated CSV (committed) -> Unity DataCatalog (thin loader).
Design + type-index + dispatch model: vault `40_Projects/Tower/50 Data Layer, Type-Index, and Dispatch Assembly.md`.

## Layout
- `Assets/_Tower/Data/Source/Tower_GameData.xlsx` — authoring source of truth (9 sheets: `_Index`/`_Schema`/`_Enums` + Marks/Passives/Abilities/Characters/Items/DropTables).
- `tools/DataSchema/Records/GameRecords.cs` — the SCHEMA as C# records (Sdp attributes). Column change = edit here.
- `Assets/_Tower/Data/Generated/*.csv` — validated extraction output, committed. Unity reads these.
- Unity loader: `Assets/_Tower/Scripts/Data/` (see `docs/tasks/T-Data.md`).

## Tooling (bluekms/StaticDataPipeline = Sdp, build-time CLIs only)
Do NOT reference `Sdp.dll` inside Unity (modern .NET / IL2CPP-incompatible). Use only the two self-contained CLIs on host/CI:
- `StaticDataHeaderGenerator` — reads `tools/DataSchema/Records/*.cs`, emits the standard header row (TSV) to paste into each Excel sheet. Keeps headers == schema.
- `ExcelColumnExtractor` — reads records + `Tower_GameData.xlsx`, extracts only the schema columns, validates (missing column / enum / `[Range]` / `[RegularExpression]` / FK), writes `*.csv`. Fails the build on any violation.

Obtain (host has .NET 10 SDK):
```
git clone https://github.com/bluekms/StaticDataPipeline.git ../_ext/StaticDataPipeline
dotnet build -c Release ../_ext/StaticDataPipeline
# or download the self-contained release exes from the repo's Releases page.
```
(Global.json in that repo pins the SDK. Keep the CLIs out of the Unity Assets tree.)

## Regen flow (after editing the xlsx or a record)
1. If columns changed: update `tools/DataSchema/Records/GameRecords.cs`, run HeaderGenerator, paste the new header row into the sheet.
2. Run `ExcelColumnExtractor` -> regenerates `Assets/_Tower/Data/Generated/*.csv` (fails on bad data).
3. Commit the CSVs. Unity re-validates on load (2nd gate) and builds the runtime DataCatalog.

## v0 status
- Records + workbook + generated CSVs seeded from the real T1 slice content.
- Extractor wiring (how records are ingested: source vs compiled assembly) to be confirmed on first run; a Python reference validator (`export_gamedata.py`, proven) exists as a fallback gate.
- `Items`/`DropTables` are placeholder schemas (no code model yet) — align when implemented.
