# Onslaught playable mission

Open **Onslaught > Play mission > Open Onslaught mission**, then press Unity's **Play** button. The scene is `Assets/Mission/Scenes/Onslaught_Mission.unity`, the enabled build scene. Click the Game view to capture the mouse.

This is the first integrated **single-player** version. It reuses the textured Mantis ship, animated enemy assets and tactical HUD. Earlier ship, enemy and HUD preview scenes remain separate and unchanged.

## Controls

| Input | Action |
| --- | --- |
| WASD / mouse | Move / look |
| Left Shift | Sprint |
| Left mouse | Fire; hold for rifle, click per pistol shot |
| Right mouse | Aim |
| R | Reload |
| E | Switch pistol / rifle; cancels an unfinished reload |
| Tab | Tactical overhead view |
| Escape | Pause / resume |
| F1 | Show / hide control guide |
| Enter after defeat or victory | Restart the mission |

Ammo and medkits are collected by walking over them. Gates cannot be opened manually. The equipment slots are reserved for future equipment; no grenades or abilities are implemented.

## Mission flow

1. Hold the ship for **240 seconds**. Enemies approach from alternating boarding ramps. Enemy variety and arrival rate increase as the countdown progresses.
2. Both **420 HP** barred gates begin closed. Enemies attack the bars until they collapse. Players can shoot through gaps; the steel bars themselves intercept bullets. HP and breach states appear below the minimap.
3. At 00:00, a voiced, filtered intercom message with subtitles warns of a powerful infected. The **Regurgitator** arrives four seconds later. Reinforcements continue during the boss fight.
4. Killing the boss immediately stops reinforcement and parasite spawning. Clear every remaining enemy and return aboard if standing on a ramp.
5. Blast shutters seal the openings and the ramps retract. An exterior view shows the ship leaving the dock, followed by the mission-complete result.

Player death stops the encounter and shows a retry prompt. Escape pauses the timer, AI, weapon actions and audio.

## Initial solo balance

These are starting values, not a claim that difficulty has been tuned through human playtesting.

| Weapon | Body damage | Magazine / initial reserve | Fire rate | Reload |
| --- | ---: | ---: | ---: | ---: |
| P-34 pistol | 34 | 12 / 120 | Up to 4 clicks/sec | 1.35 sec |
| AR-24 rifle | 24 | 30 / 540 | 8 shots/sec | 2 sec |

Regular humanoid headshots deal **2x** damage. The boss's mouth area takes **1.5x**; parasites have no headshot multiplier. A rifle begins equipped. Each weapon keeps its own magazine and reserve when switched. Guns use hitscan raycasts, recoil, moving slides, reload motion, muzzle flash, impact/tracer effects and hit confirmation.

| Enemy | HP | Speed (m/s) | Melee damage | Minimum attack interval |
| --- | ---: | ---: | ---: | ---: |
| Shambler | 100 | 1.8 | 8 | 1.25 sec |
| Sprinter | 70 | 4.6 | 7 | 1.05 sec |
| Spitter | 140 | 2.4 | 6 | 1.4 sec |
| Bloater | 320 | 1.2 | 20 | 1.9 sec |
| Regurgitator | 3,600 | 1.8 | 30 | 2.2 sec |
| Worm / Puke Worm | 20 / 35 | 2.8 / 3.1 | 3 / 5 | 1.05 / 1.15 sec |

The player has **120 HP**. Spitter acid deals 7 impact damage plus 4/sec while in the pool; boss acid deals 14 plus 8/sec. Pools last 3 and 4 seconds respectively. Avoiding an attack during its animation windup avoids its damage. Line of sight blocks attacks through walls. Bloaters release two parasites on death while reinforcements are active; the boss can vomit two.

An ammo case drops every fourth kill (+60 rifle / +24 pistol). Every tenth kill drops a medkit (+30 HP) instead. Supplies expire after 40 seconds; reserves cap at 720 rifle / 180 pistol. Supplies are deliberately generous for the initial survival loop.

Arrivals progress from one enemy every 3.2 seconds toward one every 2.2 seconds. During the boss fight the interval is 3.1 seconds. The **28 living-enemy limit includes parasites**, with a slot reserved for the boss. Parasites also retain their separate limit of 16; acid projectiles and pools cap at 16 and 12. Corpses expire after nine seconds. The minimap uses world positions and a cached floorplan without another rendering camera.

## Editing and verification

`Data/OnslaughtBalance.asset` contains duration, gate/player HP, spawn intervals and caps. Weapon and enemy balance assets sit beside it; mission enemy variants preserve the original preview definitions. `Editor/MissionBuilder.cs` authors the scene using Unity APIs and refuses to overwrite an existing mission. Edit the saved scene through the Editor.

Four NavMesh surfaces support standard, heavy, boss and parasite sizes. Dynamic gate/door geometry is excluded by modifiers; obstacles carve only while closed. The gate layer must remain in the surface layer mask so its modifier applies to the child bar colliders.

`MissionChecks.Begin()` runs the input, combat and accelerated ending checks in fresh Play mode. `MissionSoak.Begin()` observes the production four-minute countdown at normal time scale, raising only player HP so the test can survive without an operator. Both are Editor-only, temporary helpers. They write evidence under `Artifacts/` and are never saved into the mission scene.

Weapon models, barred gates, sound effects and dock geometry are original lightweight assets authored for this project. Intercom lines are generated locally with Windows speech synthesis by `ArtSource/Weapons/create_intercom.ps1`; synthetic sound effects are generated by `MissionArt.cs`. No original SAS game assets were extracted.

The existing executable under `Builds/Onslaught` belongs to the older ship walkthrough. Play this mission in Unity; a new standalone build is not implied by the scene update.
