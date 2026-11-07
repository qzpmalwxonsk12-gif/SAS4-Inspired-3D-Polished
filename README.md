# SAS4-Inspired 3D — Polished Variant

An unofficial Unity action-game prototype inspired by **SAS: Zombie Assault 4**. It reimagines mission ideas as explorable 3D environments with shooting, enemies, a tactical HUD and a squad menu.

This is a fan/learning project, not an official SAS title. Third-party names, characters and reference material remain subject to their owners' rights. No affiliation or endorsement is claimed.

## Repository status

This repository is the alternate polished snapshot. It includes the additional Vaccine polish editor pass and its associated presentation assets. It should be compared with the prototype as a variant, not assumed to be a universally newer or fully validated release.

The source includes integrated gameplay alongside older isolated previews. A preview scene does not necessarily contain the latest mission logic. Historical subsystem READMEs describe their original milestone; use the main menu for the integrated experience.

## Main systems

- **Onslaught:** ship defence, boarding approaches, destructible gates, timed pressure, boss combat and an extraction/result sequence.
- **Vaccine:** subway and laboratory environments, connected mission interactions, encounters and vaccine-recovery progression.
- **Combat:** pistol/rifle handling, firing, aiming, reloads, pickups, health and enemy behaviours.
- **Presentation:** mission HUD, minimap/radar, boss indicators, operator/loadout selection and desktop display controls.
- **Co-op source:** host/join flow, squad readiness, role selection, replication and revival helpers. The menu presents up to four squad slots.

Co-op implementation is not a guarantee that every network configuration works. Earlier testing was scoped to local development; separate-computer connectivity, latency behaviour and normal-difficulty balance need their own playtests.

## Requirements and quick start

The project records **Unity 6000.6.6f1** in [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt). Dependencies, including rendering, input and networking packages, are recorded in [Packages/manifest.json](Packages/manifest.json).

1. Add the repository folder to Unity Hub and open it with the recorded editor version.
2. Allow Unity to restore/import packages and assets.
3. Open [`Assets/Coop/Scenes/MainMenu.unity`](Assets/Coop/Scenes/MainMenu.unity).
4. Press Play and choose a mission and role. Start with solo deployment when checking a fresh checkout.
5. For co-op testing, use Host/Join and the squad-code UI, ready the players, then deploy from the host.

## Controls

The integrated Onslaught guide documents the following defaults. Context-specific interaction prompts and other review scenes can use different bindings.

| Input | Action |
|---|---|
| WASD / mouse | Move / look |
| Left Shift | Sprint |
| Left / right mouse | Fire / aim |
| R | Reload |
| E | Switch weapon in the integrated combat setup |
| Tab | Tactical overhead view |
| Escape | Pause/resume in the integrated mission |
| F1 | Toggle the control guide |

See [the mission guide](Assets/Mission/README.md) for objective flow, balance values and scene-specific details. Older walkthrough and enemy-preview controls are documented separately and should not be substituted for combat controls.

## Scenes and code map

| Path | Contents |
|---|---|
| `Assets/Coop/` | Main menu, session/role logic, replication, revival and Windows build helper |
| `Assets/Mission/` | Integrated Onslaught mission, weapons, pickups, gates and tuning |
| `Assets/Vaccine/` | Vaccine scenes, interactions, encounters and editor construction passes |
| `Assets/Enemies/` | Enemy definitions, behaviour, prefabs and preview tools |
| `Assets/HUD/` | Tactical HUD, map/radar and preview scene |
| `Assets/Onslaught/` | Original ship walkthrough and environment work |
| `ArtSource/` | Editable/reference art and provenance material retained in the source snapshot |

Useful isolated scenes include [Onslaught mission](Assets/Mission/Scenes/Onslaught_Mission.unity), [Vaccine blockout](Assets/Vaccine/Scenes/Vaccine_Blockout.unity), [laboratory district](Assets/Vaccine/Scenes/Vaccine_LabDistrict.unity), [enemy preview](Assets/Enemies/Scenes/EnemyPreview.unity) and [HUD preview](Assets/HUD/Scenes/HUDPreview.unity).

## Building a Windows player

Use Unity's build settings with the intended enabled scenes and Windows x86-64 target. The included [`CoopBuild.Build`](Assets/Coop/Editor/CoopBuild.cs) helper builds the enabled scene list to `Builds/SAS4-3D/SAS4-3D.exe` and writes a build summary under `Artifacts/Coop`. Confirm the enabled scene order before using it; generated builds are not included in this repository.

## Validation and limitations

Source checks and editor test/probe scripts live with their corresponding systems, including `Assets/Mission/Tests`, `Assets/Vaccine/Tests` and `Assets/Coop/Tests`. Run relevant checks in the project/editor setup before treating a change as validated. This README update did not rebuild the game, retest multiplayer or verify performance on a second machine.

This is an evolving prototype. Some preview guides are historical, balance needs human testing, and game-version/package changes may affect import. Consult [BACKUP_NOTES.md](BACKUP_NOTES.md) for omitted caches, build outputs and other files. Do not assume every local development artifact or reference asset was packaged in the GitHub snapshot.
