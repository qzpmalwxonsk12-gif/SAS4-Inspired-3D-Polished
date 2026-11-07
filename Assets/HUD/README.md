# Ship HUD

Open **Onslaught > HUD > Open HUD preview**, then press **Play** and click the Game view.
Scene: `Assets/HUD/Scenes/HUDPreview.unity`. Reusable UI: `Assets/HUD/Prefabs/ShipHUD.prefab`.

This separate preview displays four animated enemy assets on short, clear routes. Their radar positions are real; combat and navigation are inactive here. The integrated game is now in `Assets/Mission/Scenes/Onslaught_Mission.unity`, where this HUD shows real gun ammunition, player/enemy health, gate integrity and mission progress. The original ship walkthrough and enemy behavior arena remain available.

## Presentation

- Upper-left north-up ship map, baked from the actual deck meshes and wall colliders. Cyan arrow and view cone show player position/heading. Filled red pips are enemies; the larger hollow red diamond is the boss. Amber diamond marks the boarding approach. Door/gate lines are amber when closed and cyan when open.
- Lower-left numeric health and segmented bar, with wounded/critical/dead text and a brief damage-edge flash. Green normal health and red critical health do not rely on color alone.
- Lower-right weapon, magazine, reserve, low-ammo/empty status and reload progress. The preview rifle is explicitly marked **PREVIEW**, with simulated ammunition; it does not fire.
- Objective, mission elapsed time, location, crosshair, existing gate prompt, optional boss-health panel and empty equipment slots. Equipment is a non-interactive placeholder, with no invented active abilities.
- F1 hides/shows the movement guide. All HUD graphics have raycast targets disabled.

## Preview controls

| Input | Effect |
|---|---|
| WASD / mouse / Shift | Walk, look, sprint |
| E | Existing boarding-gate interaction |
| Tab | Existing overhead view |
| Escape / left click | Release / capture mouse |
| Enter | Reset walking position |
| F1 | Toggle movement guide |
| F6 | Take 25 preview damage |
| F7 | Cycle low ammo, timed reload, unarmed display |
| F8 | Toggle Regurgitator preview and health bar |
| F9 | Restore health/ammunition and hide boss |

## Gameplay connections

`ShipHud` exposes `playerHealth`, `SetWeapon(title, rounds, magazineCapacity, reserve)`, `SetReload(active, progress)`, `SetBoss(vitality, title)` and `SetMission(title, objective, waveText)`. The mission's `PlayerWeapons` and `MissionDirector` bind these values. `externalClock` lets the director supply the four-minute countdown. Health and boss health read `Vitality`. An unbound weapon displays `UNARMED / --`, and unbinding clears an active reload display.

Assign the player's transform, scene doors, objective transform and `HudMapData` to `HudRadar`. Active, living `EnemyBrain` instances are picked up automatically through the existing registry. `MinimapContact` is an optional adapter for other hostile actors and for this preview. An actor with both is counted once. Dead, disabled, destroyed and out-of-map contacts are omitted. A 128-contact buffer prioritizes bosses and nearer enemies, while the counter reports all in-map contacts. This is whole-ship tactical awareness, not a line-of-sight or discovery system.

The UI prefab contains no preview driver, preview actors or scene-object references. `HudPreview` and `HudPreviewActor` belong only in the review scene. Remove or replace the preview controls before gameplay integration. The elapsed timer is scene time, not a mission countdown.

## Low-end hardware

uGUI / TextMeshPro, shared default UI materials, vector geometry and a static floorplan. The minimap has no extra camera, render texture, texture atlas or per-frame object creation. Contact geometry refreshes at 10 Hz on a nested canvas; marker storage is fixed. Only four moving enemy displays are active by default; F8 adds one boss. This is a design budget, not a measured promise of target hardware frame rate.

The map data is a bake of the current ship layout. If geometry changes later, rebake its floor/wall data through the Editor; do not hand-edit scene or prefab YAML. `HudBuilder.Build()` records how the preview was authored and refuses to overwrite an existing preview.

## Reference and verification

Visual cues were checked in [Sergrim's Onslaught gameplay](https://www.youtube.com/watch?v=_rdg9AqzBEU), at approximately 5:40: compact health/ammo readouts, angular panels and a red-contact minimap. The HUD adapts these ideas for a first-person view. All panel shapes, schematic icons and map geometry are original code-drawn graphics; no SAS4 artwork was extracted.

`Artifacts/HUD-VALIDATION.md` records the checks. `HudChecks.Begin()` runs the live Play-mode checks through the official Unity tools, with temporary Input System devices and cleanup. The helper is Editor-only and is not saved in the scene.
