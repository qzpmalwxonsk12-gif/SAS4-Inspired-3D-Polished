# Enemy asset kit

Original low-poly 3D interpretations of SAS: Zombie Assault 4's normal enemies. This is an isolated asset and behavior milestone: the Onslaught mission, its walker, guns and game HUD have not been connected to these enemies.

## Try the assets

In Unity, choose **Tools > Enemies > Open asset preview**, then press **Play**. Scene: `Assets/Enemies/Scenes/EnemyPreview.unity`.

| Control | Action |
|---|---|
| 1 / 2 / 3 / 4 / 5 | Inspect Shambler / Sprinter / Spitter / Bloater / Regurgitator |
| 6 / 7 | Inspect Worm / Puke Worm |
| 0 | Return to the full lineup; clear the behavior demo |
| Right mouse drag / wheel | Orbit / zoom the selected display |
| Space | Cycle Idle, Move, Melee, Hit, Death animations |
| E | Play Spit / Vomit where available, otherwise Melee |
| H / K / R | Flinch / death / idle on a display |
| Tab | Spawn the selected enemy in the navigation arena |
| WASD in the arena | Move the capsule target |
| H / K / R in the arena | Damage / kill / restart the active enemy |

Click the Unity Game view so it receives keyboard input. These are asset-review controls, not the future game's HUD or combat controls. The arena target has deliberately high health for repeated observation. The central wall tests visibility and navigation. Stop Play to return to the saved, empty arena.

## Contents

- Seven reusable prefabs in `Prefabs/`, plus acid projectile and acid pool prefabs.
- Seven adjustable `EnemyDefinition` assets in `Definitions/`.
- Seven original skinned FBXs in `Models/`, with editable `.blend` sources in `ArtSource/Enemies/` at the project root.
- Idle, Move, Melee, Hit and Death clips for every creature; Spit for Spitter and Vomit for Regurgitator. Generic rigs, in-place motion, no ragdolls.
- Shambler limping gait; faster Sprinter gait; heavier Bloater and boss animation timing; segmented worm movement.
- One 1024-pixel compressed, mipmapped skin/muscle/mutant/cloth atlas, plus simple rough materials for teeth, mouth interiors, cleaver and acid.

| Enemy | Appearance and behavior |
|---|---|
| Shambler | Pale decayed humanoid in worn work clothing; slow pursuit and delayed melee. |
| Sprinter | Lean, exposed muscle; fast pursuit and faster attack recovery. This is the SAS4 **Runner** role, retained under the user's requested name. |
| Spitter | Mutant skin with swollen throat and shoulder glands; aimed acid projectile, floor pool and weaker close melee. |
| Bloater | Distended belly, heavy limbs and cleaver; slow turning and heavy melee; releases four Worms on death. |
| Regurgitator | Large blue-green body, no conventional head, recessed red maw and oversized shoulders; acid vomit plus three Puke Worms, heavy melee, six additional Puke Worms on death. |
| Worm / Puke Worm | Small segmented parasites; inherit the spawning enemy's target and pursue it. |

Projectile impact, acid damage over time, death-spawn counts and combat damage are deliberately adjustable. Health and movement values use reference base values where sources agree, but this is not a frame-exact simulation of Flash or mobile SAS4. Attack ranges and damage were adapted for a readable 3D prototype. No Apex, Elite, Savage, evolved or Resurgence variants are included.

## Integration later

1. Add an appropriate baked NavMesh to the mission for each required agent type: **Enemy standard**, **Enemy heavy**, **Enemy boss**, **Enemy parasite**. Their radii and heights differ; a boss must not inherit humanoid doorway clearance.
2. Instantiate a prefab at a compatible NavMesh position. Keep `displayOnly` false.
3. Assign the intended target with `EnemyBrain.SetTarget(Vitality)`. No automatic player search or coupling to the ship controller exists.
4. The target's `Vitality` should have `receivesEnemyAttacks` enabled and the desired health and aim height. Weapon hit detection can call the enemy's `Vitality.TakeDamage(amount, DamageKind)`.
5. `Damaged` / `Died` events and the `MouthSocket` transform are available for later sound, feedback, drops and encounter integration. Audio and final combat balancing remain for that pass.

Damage is checked at the attack impact time, including current reach and line of sight. Death disables navigation and collision. Corpses disappear after nine seconds. Acid expires after five seconds (seven for the boss). Parasites are capped at **16 alive**, projectiles at **16**, and pools at **12** across this kit. Navigation destinations refresh every 0.2 seconds. The preview uses shadowless lighting and no post-processing; no large horde performance claim has been made.

## Rebuild and verify

`ArtSource/Enemies/build_enemies.py` regenerates the original Blender sources and FBX exports with Blender 5.2.2 LTS. **Tools > Enemies > Rebuild enemy assets and preview** configures import settings, materials, controllers, definitions, prefabs and the isolated test scene through Unity Editor APIs. Save your current scene and stop Play first. Rebuilding intentionally resets this generated kit's tuning and preview layout; edit the builder for lasting generated changes, or duplicate assets for hand-authored variants.

The builder adds four named navigation agent types while preserving existing types. It does not change the Onslaught scene or the build scene list.

With the preview playing, Editor automation can invoke `ShipEnemies.EnemyChecks.Begin()`. Results are written to `Artifacts/enemy-checks.txt` at the project root. The checks cover imported references and clips, bone motion across multiple frames, navigation clearance and wall routing, melee timing and dodging, visibility, projectile/pool damage and expiry, parasites and spawn caps, resistance, cleanup, and actual Input System preview controls. The test component is excluded from player builds.

Research, gameplay segments and the generated texture prompt are recorded in `ArtSource/Enemies/RESEARCH_AND_PROVENANCE.md`.
