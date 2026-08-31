# Tower — Gen 1 reference asset

> **Status (2026-08-31):** this repository is the completed Tower **Gen 1 implementation reference**, not the landing target for the current product.
>
> On 2026-08-21 the owner confirmed a separate AA action game as **Tower Gen 2**. Its GitHub repository has not been created yet. See the [Tower hub](https://app.notion.com/p/3ba194007a54814b9404cf6bad57a812) and the [Gen 2 decision](https://app.notion.com/p/3c3194007a548196aaa3e14b1c7755e1).

Gen 1 explored a single-player 3D Tower crawler in Unity 6 for Windows/Steam. It completed the structural run loop and automated completion gate, but it was retired as the active product because it did not prove the intended game feel.

## What remains authoritative here

- Historical Gen 1 code, task briefs, tests, scenes, assets, and build contracts.
- T62 evidence for the fresh-run → encounters → boss → conquest → save → resume flow.
- Engineering lessons, failure modes, and validation-gate patterns that may inform Gen 2.
- The implementation state of Gen 1 at its final `main` commit.

Gen 1 code is **not** automatically the implementation base for Gen 2. Durable lore, design pillars, decision history, failure notes, and gate discipline are selected separately through Notion and the local Tower vault.

## Repository policy

- Do not start new gameplay, Unity scene, prefab, asset, or feature work in this repository.
- Allow only archival state corrections, security maintenance, and evidence-preserving fixes.
- Do not interpret an old task brief or `AGENTS.md` summary as the current Gen 2 product direction.
- Keep Unity/runtime claims tied to the original build and test evidence; documentation-only work does not create new runtime verification.

## Historical environment

- Unity: **6000.3.19f1**
- Target: PC (Windows), Steam
- Git LFS required: run `git lfs install` before clone/pull
- Historical design integration point: local vault `Personal Agent Memory/40_Projects/Tower/`
