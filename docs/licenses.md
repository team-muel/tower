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
| `Assets/_Tower/Art/Textures/asphalt_01/` | Poly Haven `asphalt_01`, https://polyhaven.com/a/asphalt_01 | CC0, verified via https://polyhaven.com/license | Road/asphalt floor material for `_FloorPreview` and future Seoul-hill road modules | Imported JPG maps: albedo, AO, normal, roughness. Unity material `M_Asphalt_PH`. |
| `Assets/_Tower/Art/Textures/gravel_floor_02/` | Poly Haven `gravel_floor_02`, https://polyhaven.com/a/gravel_floor_02 | CC0, verified via https://polyhaven.com/license | Dirt/gravel route material for `_FloorPreview` | Imported JPG maps: albedo, AO, normal, roughness. Unity material `M_Dirt_PH`. |
| `Assets/_Tower/Art/Textures/brick_moss_001/` | Poly Haven `brick_moss_001`, https://polyhaven.com/a/brick_moss_001 | CC0, verified via https://polyhaven.com/license | Mossy brick/concrete proxy for sensory lane smoke tests | Imported JPG maps: albedo, ARM, normal. Unity material `M_BrickMoss`. |
| `Assets/_Tower/Art/M_Asphalt_PH.mat` | Derived Unity material | Project-authored derivative of Poly Haven source maps | Preview asphalt material | Keep paired with `asphalt_01` ledger row. |
| `Assets/_Tower/Art/M_Dirt_PH.mat` | Derived Unity material | Project-authored derivative of Poly Haven source maps | Preview dirt/gravel material | Keep paired with `gravel_floor_02` ledger row. |
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
