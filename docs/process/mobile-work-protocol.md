# Tower cross-system status protocol

## Purpose

Keep Notion, Linear, GitHub, the local Tower vault, and Unity evidence from describing different product generations as if they were one active implementation.

## Current generation record

| Generation | Repository | Product status | Implementation status |
| --- | --- | --- | --- |
| Gen 1 | `team-muel/tower` | Reference asset; ended 2026-08-21 | Historical Unity implementation preserved |
| Gen 2 | Not created | Current AA action game | Main product start decided; repository and Unity project not started |

Gen 1 completed its structural run loop and T62 completion gate, but it did not prove the intended game feel. Gen 2 is a separate main-game implementation, not a continuation branch in this repository.

## Surface responsibilities

- **Notion Tower:** owner decisions, generation table, mobile intake, design maturity, and unresolved questions.
- **Linear:** priority and work state. Gen 1 and Gen 2 must use separate issues.
- **GitHub Gen 1:** historical code, tasks, and evidence. It is not a Gen 2 landing target.
- **Future GitHub Gen 2:** implementation source of truth after the repository is explicitly created.
- **Local Tower vault:** durable integration history. When unavailable, record `sync pending`.
- **Unity build/test evidence:** runtime truth for the exact Git SHA, Unity revision, build, and hardware used.

## Required status fields

Every Tower task or decision should record:

1. **Product generation:** Gen 1 reference / Gen 2 current
2. **Source authority:** owner input / agent proposal / external reference
3. **Design maturity:** exploration / owner decision / task brief
4. **Synchronization:** pending / cross-checked / superseded
5. **Implementation:** not started / branch / draft PR / merged / Unity verified

A decision may be authoritative without being implemented. A merged document may be synchronized without being Unity verified.

## Mobile and non-Unity work

Without Unity, the following may proceed for Gen 2 after an explicit task is opened:

- Owner-decision capture and research
- Task briefs and acceptance criteria
- Data schemas and deterministic pure-code contracts
- Draft PRs in the future Gen 2 repository
- Test and telemetry design

The following cannot be marked complete without a Unity developer or runner:

- Scene, prefab, Animator, material, shader, VFX Graph, APV, and import work
- Unity compilation and Test Runner results
- Windows build validation
- Visual, interaction, game-feel, and performance claims

Do not place Gen 2 experiments in the Gen 1 repository merely because it already contains a Unity project.

## Gen 1 archive rule

Allowed Gen 1 changes are archival corrections, security maintenance, and evidence-preserving fixes. New gameplay or asset work is out of scope.

Historical branches and closed/unmerged PRs remain evidence, not backlog. In particular:

- T77 / PR #61 is a preserved AA exploration input for Gen 2 and is not a Gen 1 landing target.
- T78 / PR #62 is the final documentation-only state alignment for Gen 1.
- The former stacked-PR landing plan and T63/T64 convergence roadmap are not active.

## Gen 2 bootstrap gate

Do not create the Gen 2 repository or Unity project from an inferred next step. Begin only after an explicit owner instruction that resolves at least:

- Repository name
- Unity version
- Reference hardware and resolution
- First game-feel slice
- Which durable documents and gate rules are copied

Gen 1 code transfer is opt-in and justified file by file; it is not the default.

## Handoff rule

When a task changes state:

1. Update its Linear issue.
2. Link the Notion decision or work packet.
3. Link the GitHub branch/PR and exact verification evidence.
4. Return Unity results with Git SHA, Unity revision, build identity, logs, and captures.
5. Mark unavailable surfaces as `sync pending` instead of implying completion.
