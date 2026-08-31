# Tower Gen 1 — Agent Archive Contract

## Current status

This repository is the **Gen 1 implementation reference**. It stopped being the active Tower product on **2026-08-21**.

The current product is a separate AA action game, **Tower Gen 2**. Its GitHub repository has not been created. The current product decision is recorded in:

- [Notion Tower hub](https://app.notion.com/p/3ba194007a54814b9404cf6bad57a812)
- [2026-08-21 Gen 2 decision](https://app.notion.com/p/3c3194007a548196aaa3e14b1c7755e1)

Do not treat this repository's old 3D-grid-turn-based summary, task queue, branches, or unmerged implementation stack as current Gen 2 direction.

## Allowed work in this repository

- Archival status and provenance corrections
- Security maintenance
- Evidence-preserving fixes required to keep historical material readable
- Documentation that clearly separates Gen 1 history from Gen 2 decisions

## Work that must not start here

- New gameplay features
- New Unity scene, prefab, material, animation, VFX, APV, or asset work
- Gen 2 prototypes or production code
- Resuming the historical stacked PR landing plan
- Reclassifying documentation-only changes as Unity/runtime verification

Any future Gen 2 implementation requires an explicit owner instruction and a separate repository.

## Source responsibilities

- **Notion Tower:** current mobile intake, owner decisions, generation table, and cross-system status.
- **Local Tower vault:** durable design history and local integration record. If unavailable, record **sync pending**; never claim it was updated.
- **This GitHub repository:** historical Gen 1 code and evidence only.
- **Build/test artifacts:** historical runtime truth for the commit that produced them.

The four-axis record remains useful:

1. Source authority
2. Design maturity
3. Synchronization
4. Implementation

For repository work, also state the product generation: **Gen 1 reference** or **Gen 2 current**.

## Preserved Gen 1 facts

- Unity version: **6000.3.19f1**
- Gen 1 moved from the original grid/turn slice toward continuous-space real-time party combat.
- T62 completed the structural run gate: fresh run → scheduled encounters → boss → conquest → save → resume.
- Gen 1 ended because it did not prove the intended game feel, not because its structural loop was absent.
- Later task records may contain validation that was pending when Unity was unavailable; keep those claims scoped to their recorded evidence.

## Gen 2 carryover rule

Do not copy Gen 1 code by default. Candidate carryover is limited to deliberately selected:

- Worldbuilding and design pillars
- Owner decision history
- Failure and tooling trap notes
- Determinism, test, build, and evidence-gate discipline

Selection belongs to the Gen 2 bootstrap task and its future repository.

## Historical validation reference

Gen 1 used Unity batchmode compile, EditMode tests, Windows builds, desktop run logs, screenshots, and deterministic QA state. These are valuable process references but do not validate a future Gen 2 implementation.

See `docs/process/mobile-work-protocol.md` for the current cross-system status protocol.
