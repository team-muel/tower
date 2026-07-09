# Tower Art and Asset License Ledger

Last verified: 2026-07-07.

This file is the repo-local gate for non-code assets. It does not replace the vault art plan; it records what can be committed to `Assets/_Tower/Art` and what evidence must travel with future asset PRs.

## Import Rules

1. Prefer sources that are CC0/public-domain equivalent and do not require account credentials.
2. Every committed third-party asset folder must be listed in the asset ledger below before merge.
3. Store textures under `Assets/_Tower/Art/Textures/<source_asset_id>/` and Unity materials under `Assets/_Tower/Art/Materials/` unless an existing scene-specific pattern is already established.
4. Keep original source IDs in filenames when practical. Do not rename a downloaded source beyond suffix normalization such as `_albedo`, `_normal`, `_rough`, `_ao`, or `_arm`.
5. For mixed-license sites, do not commit an asset until its individual asset page/license is recorded in the ledger.
6. Do not commit credentials, generated API keys, account screenshots, or files whose source terms are unclear.
7. Screenshot references and external concept images are references only. They are not source assets for redistribution.

## Approved Source Classes

| Source | Default status | Use in Tower | Evidence required |
|---|---|---|---|
| Poly Haven | Allowed | PBR textures, HDRIs, 3D models | Asset URL plus Poly Haven license URL |
| ambientCG | Allowed | PBR texture alternatives for concrete, asphalt, walls, roofs | Asset URL plus ambientCG license URL |
| Kenney | Allowed | Low-risk placeholder game assets and UI/audio packs | Asset page URL plus included license or support/license page |
| Quaternius | Allowed with asset-page check | Low-poly proxy buildings, props, characters | Asset page URL showing free/commercial or CC0 terms |
| Pretendard | Allowed for Korean UI font work | Korean UI typography | Repository/license URL |
| Freesound | Conditional | SFX only | Individual sound URL and exact license; avoid noncommercial or unclear attribution chains |
| OpenGameArt | Conditional | Fallback sprites/SFX/models | Individual asset URL and exact license; avoid GPL/CC-BY-SA unless the project deliberately accepts obligations |
| Mixamo | Conditional | Character animation prototyping | Adobe/Mixamo terms evidence; do not redistribute standalone raw animation packs outside the game project |

## Current Asset Ledger

| Asset folder or file | Source | License | Local use | Processing notes |
|---|---|---|---|---|
| `Assets/_Tower/Art/Textures/asphalt_01/` | Poly Haven `asphalt_01`, https://polyhaven.com/a/asphalt_01 | CC0, verified via https://polyhaven.com/license | Road/asphalt floor material for `_FloorPreview` and future Seoul-hill road modules | Imported JPG maps: albedo, AO, normal, roughness. T37 generated `asphalt_01_mask.png` as R=0 metallic, G=AO, B=1 detail mask, A=1-roughness smoothness. Unity material `M_Asphalt_PH`. |
| `Assets/_Tower/Art/Textures/gravel_floor_02/` | Poly Haven `gravel_floor_02`, https://polyhaven.com/a/gravel_floor_02 | CC0, verified via https://polyhaven.com/license | Dirt/gravel route material for `_FloorPreview` | Imported JPG maps: albedo, AO, normal, roughness. T37 generated `gravel_floor_02_mask.png` as R=0 metallic, G=AO, B=1 detail mask, A=1-roughness smoothness. Unity material `M_Dirt_PH`. |
| `Assets/_Tower/Art/Textures/brick_moss_001/` | Poly Haven `brick_moss_001`, https://polyhaven.com/a/brick_moss_001 | CC0, verified via https://polyhaven.com/license | Mossy brick/concrete proxy for sensory lane smoke tests | Imported JPG maps: albedo, ARM, normal. Unity material `M_BrickMoss`. |
| `Assets/_Tower/Art/M_Asphalt_PH.mat` | Derived Unity material | Project-authored derivative of Poly Haven source maps | Preview asphalt material | Keep paired with `asphalt_01` ledger row. T37 connects the generated mask to `_MetallicGlossMap` and `_OcclusionMap`. |
| `Assets/_Tower/Art/M_Dirt_PH.mat` | Derived Unity material | Project-authored derivative of Poly Haven source maps | Preview dirt/gravel material | Keep paired with `gravel_floor_02` ledger row. T37 connects the generated mask to `_MetallicGlossMap` and `_OcclusionMap`. |
| `Assets/_Tower/Art/Materials/M_BrickMoss.mat` | Derived Unity material | Project-authored derivative of Poly Haven source maps | Brick moss material | Keep paired with `brick_moss_001` ledger row. |

## Rejected Or Needs Human Review

| Source class | Reason |
|---|---|
| AI-generated assets from paid/BYOK services | Accept only after the user confirms account terms and local storage path. Record provider, prompt, date, and license/export terms. |
| S-Map or other city/cadastre screenshots | Reference only unless redistribution terms are explicitly cleared. Use them for shapes, density, color, and layout language, not as texture/model sources. |
| LUMA Arles or other copyrighted architectural photos | Reference only. Tower massing must be original and transformed, not a direct model or texture copy. |
| Random web images | Not allowed unless license and source provenance are documented before import. |

## Future PR Checklist

- Add or update a row in `Current Asset Ledger`.
- Link the source page and license page.
- State whether files are original downloads, resized, channel-packed, normal-map reimported, or Unity-authored derivatives.
- Run Unity import/compile checks when adding files under `Assets/`.
- Keep large temporary screenshots outside git unless the task explicitly requires them.

### T44 additions (2026-07-09) — CC0 asset lane

| Asset folder or file | Source | License | Local use | Processing notes |
|---|---|---|---|---|
| `Assets/_Tower/Art/Nature/Quaternius/` (49 FBX) | Quaternius "Ultimate Nature Pack" (150 models), https://quaternius.itch.io/150-lowpoly-nature-models | CC0 1.0 (bundled `License.txt`: "CC0 1.0 Universal") | Forest biome props: trees (Common/Birch/Pine/Willow), rocks, mossy rocks, bushes, berry bushes, grass, plants, flowers, stumps, logs | Subset copied from pack FBX folder. Import: `useFileScale=true`, `bakeAxisConversion=true`. Models carry root rot (90,0,0) + scale 100 — wrapped in identity-root prefabs. No textures (flat per-material colors). |
| `Assets/_Tower/Art/Creatures/Quaternius/` (4 FBX) | Quaternius "LowPoly Animated Monsters", https://quaternius.itch.io/lowpoly-animated-monsters | CC0 1.0 (itch.io asset page: "CC0 License") | Enemy/boss placeholders: Slime, Bat, Skeleton, Dragon | `animationType=Generic`, `importAnimation=true`. 4–5 clips each (Idle/Walk/Attack/Death/Hit). Prefab scale 0.375 (0.60 for Dragon) → Skeleton 1.92m = player height. |
| `Assets/_Tower/Art/Props/Kenney/kn_*.fbx` (15) | Kenney "Nature Kit", https://kenney.nl/assets/nature-kit | CC0 (bundled `License.txt`: "Creative Commons Zero, CC0") | Anchor/marker props: sign, statues (obelisk/ring/head/block/column), tall stone, mushrooms, logs, stump, stone path, campfire stones | FBX + per-material colors. Prefab root scale 1.8–3.0 (Kenney kits are ~half real scale). |
| `Assets/_Tower/Art/Props/Kenney/ks_*.fbx` (10) | Kenney "Survival Kit", https://kenney.nl/assets/survival-kit | CC0 (bundled `License.txt`) | Anchor props: chest, resource wood/stone, floor hole (trap), signpost, barrel, box, tent, bedroll, campfire pit | FBX shares one `colormap` atlas. Prefab root scale 2.5. |
| `Assets/_Tower/Art/Props/Kenney/Textures/kenney_colormap.png` | Kenney Survival Kit atlas | CC0 | Base map for `M_K_colormap` | TextureImporter: `filterMode=Point`, `wrapMode=Clamp` (palette atlas — bilinear bleeds neighbouring swatches). |
| `Assets/_Tower/Art/Materials/Shared/M_Q_*.mat`, `M_K_*.mat` (39) | Derived Unity materials | Project-authored derivatives of the CC0 sources above | Shared URP Lit materials remapped onto every FBX via `ModelImporter.AddRemap` | One material per source material name, deduplicated across 78 models (avoids 78× embedded copies). `_Smoothness=0.08`, `_Metallic=0`. Flat-colour by design — the painterly post pass supplies the DE look. |
| `Assets/_Tower/Art/Textures/forest_leaves_02/` | Poly Haven `forest_leaves_02`, https://polyhaven.com/a/forest_leaves_02 | CC0, verified via https://polyhaven.com/license | Forest floor ground material (1층계 숲) | 2K JPG: diffuse, nor_gl, arm, disp. `nor_gl` = OpenGL normal (import as Normal map). `arm` = AO(R)/Rough(G)/Metal(B). |
| `Assets/_Tower/Art/Textures/bark_willow/` | Poly Haven `bark_willow`, https://polyhaven.com/a/bark_willow | CC0, verified via https://polyhaven.com/license | Tree bark detail material | 2K JPG: diff, nor_gl, arm, disp. |
| `Assets/_Tower/Art/Fonts/Pretendard/` | Pretendard v1.3.9, https://github.com/orioncactus/pretendard | **SIL OFL-1.1 — NOT CC0** | Korean runtime UI / tooltips / QA overlay | Regular + Bold OTF only. `LICENSE-OFL.txt` committed alongside and must stay. Embedding/bundling in the game is permitted; selling the font standalone is not. |

**Prefab layer (project-authored):** `Assets/_Tower/Prefabs/Art/{Nature,Anchors,Creatures}/` — 64 wrapper prefabs. Each has an identity-transform root (scale 1, rot 0) with the source FBX as a child, and the child offset so world `minY = 0` (foot-on-ground pivot). This lets `ForestFloorRenderer` / scene placement set position, yaw and uniform scale freely without fighting the FBX root's baked (90,0,0)/×100 transform.

**Trap log (new, all hit for real):**
1. Quaternius FBX root carries rot (90,0,0) + scale 100. Any code that overwrites `transform.rotation` on the model prefab root lays the tree down. Wrapper prefabs are the fix; `bakeAxisConversion` alone does not remove the root rotation.
2. `Renderer.bounds` on an un-instantiated prefab asset is meaningless. Measure by `InstantiatePrefab` into the scene **without** resetting the transform.
3. `ModelImporter.useFileScale=false` makes `globalScale` multiply *raw* FBX units, not metres. Quaternius raw ≈ centimetres, Kenney raw ≈ metres — a single `globalScale` cannot serve both. Keep `useFileScale=true` and scale at the prefab root.
4. Kenney `colormap` atlas needs Point filtering or adjacent palette swatches bleed into each prop's faces.
5. Pretendard is OFL-1.1, not CC0 — do not lump it into the CC0 row.
