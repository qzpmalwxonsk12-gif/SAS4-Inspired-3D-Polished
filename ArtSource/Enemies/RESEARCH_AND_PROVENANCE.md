# Research and asset provenance — 21 October 2026

## Sources consulted

Community reference pages (not an official game specification):

- [Shambler](https://saszombieassault.fandom.com/wiki/Shambler): slow basic humanoid; reference base health 95 and movement 1.7 m/s.
- [Runner](https://saszombieassault.wiki.gg/wiki/Runner): fast, largely skinless humanoid; reference base health 175 and movement 4.2 m/s. The requested Sprinter name is used for this SAS4 role; Sprinter is also associated with the earlier game.
- [Spitter](https://saszombieassault.wiki.gg/wiki/Spitter): ranged acid ball and persistent damaging pool; base health 115, movement 3 m/s, spit cooldown 2.7 seconds, chemical resistance 10 percent.
- [Bloater](https://saszombieassault.wiki.gg/wiki/Bloater): slow, bulky, high-health cleaver carrier, releasing Worms on death; base health 1100 and movement 1.05 m/s.
- [Regurgitator](https://saszombieassault.wiki.gg/wiki/Regurgitator): large blue-green boss with central red maw, acid and Puke Worm regurgitation, further worms on death; 1.5 m/s, 8.5-second vomit cooldown and 20 percent chemical resistance. Health differs between sources and game editions; this kit uses 7500 as an editable starting value.
- [Worms](https://saszombieassault.wiki.gg/wiki/Worms): Bloater parasites and stronger Puke Worm variant; color differs by game edition.

Several wiki pages denied direct browsing; indexed page excerpts supplied the behavior information. No claim is made that all editions share identical stats.

## Gameplay inspected

Selected frames/segments were visually inspected in the browser; these videos were not watched in full or downloaded.

- [SAS Zombie assault 4 1887 Shockfield vs regurgitator LV25 — adlbadwa](https://www.youtube.com/watch?v=PhWuMBxSSwQ), approximately 0:05, 0:46 and 1:21. Mobile combat, acid and parasites; the boss was small/partly obscured.
- [S.A.S Zombie Assault: 4 | All bosses | basic bosses — Abnormal.S.W.A.T](https://www.youtube.com/watch?v=izUTSlI145k), approximately 0:25–0:50. The normal Regurgitator was visible near 0:40–0:50: large blue-green body, broad shoulders and arms, red central maw, acid and parasites. This was the strongest boss silhouette reference.
- [Gameplay PC: SAS: Zombie Assault 4 (Mission 1 Onslaught) — Sergrim](https://www.youtube.com/watch?v=_rdg9AqzBEU), approximately 4:43 onward. Onslaught deck and approaching enemies were inspected. The footage includes high-level variants, so it was used for movement/context rather than exact normal-enemy costume details.

## Interpretation

All geometry, bone rigs and keyframed animations were created locally in Blender. These are original low-poly interpretations for this fan prototype, not extracted game assets or exact model reproductions. Normal variants are the target. The small boss maw, swollen torso, cleaver and chemical glands communicate roles in a first-person 3D space.

Health/movement reference values are starting points. Melee damage, 3D ranges, animation timing, parasite counts, acid damage and lifespan are project choices. No original game sounds, textures, sprites or model files were copied. Audio is not part of this asset pass.

## Generated texture

Mode: **built-in image generation**, new bitmap, no input/reference image and no downloaded artwork. One shared texture atlas uses four different surface regions; UVs select the relevant material family. Unity imports it with a 1024 maximum size, compression and mipmaps.

Final project asset: `Assets/Enemies/Textures/EnemySurfaceAtlas.png`.

Prompt used verbatim:

> Use case: stylized-concept. Asset type: 1024 square game surface texture atlas for low-poly sci-fi zombie enemy models. FOUR exact equal quadrants, no labels, no borders or padding. Upper-left: sickly pale grey-green dead skin, subtle bruises and thin veins, dry leathery pores. Upper-right: muted maroon and rusty pink exposed sinewy muscle fibers, non-graphic stylized surface detail, no organs. Lower-left: swollen mottled blue-green leathery mutant skin, dull olive capillaries, sparse purplish bruising. Lower-right: worn desaturated dark navy work-cloth fabric, fine weave, small grime variations, no seams, no buttons. Each quadrant is a continuous flat surface sample that fills the quadrant edge to edge, no anatomy or recognizable body parts, no lighting or shadows, no perspective, no highlights, low-contrast hand-painted realistic texture suitable for a serious rugged sci-fi horror game. No cartoons, no neon, no text, no model renders, no actual figures. Fine surface grain rather than conspicuous repeat patterns.

Editable mesh/animation sources: the seven `.blend` files in this folder. Reproducible authoring script: `build_enemies.py`. Triangle/bone/clip counts: `model-report.json`.
