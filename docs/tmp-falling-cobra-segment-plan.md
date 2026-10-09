<plan-for-suite-preface>
Before working from this file, invoke `plan-for-suite-execution` skill through the `Skill` tool.
Ask whether to run under `cascade-all-movements` or `halt-each-movement`, and follow the chosen skill for how the Movements proceed.
</plan-for-suite-preface>

# Plan: Falling Cobra Tied to Tree Segments

Branch: `fix-falling-cobra-spawn` (already a feature branch). No commit is part of this Plan; AGENTS.md requires Unity closed before any commit.

## Current State

- `FallingCobraSpawner` (on `DTSPool` in `Lv1Scene`) is one global timer: unlock at 60 u climb, one cobra at a time, picks a `Branch`-tagged object 4 to 9 u above the monkey.
- `FallingCobra` warns on `Start`, then falls at a fixed 6 u/s (prefab field) straight down, destroys itself 5 u below the monkey.
- Branches are rolled per segment in `DynamicTreeSegmentPrefabRoot` (attempt count x ramped-difficulty probability curve, both in `GameBalanceConfig`). `Restart` destroys every non-prefab child, so anything parented to a segment is recycled with it.
- `DTSPool` already calls `SpiderWebSpawner.PopulateSegment` after the first stack and after each recycle. The cobra spawner reuses that pattern.

## Target Behavior

- A separate spawner component rolls cobras per segment: fixed attempt count, each attempt against a probability curve sampled at the segment's ramped difficulty.
- Each cobra is a child of its segment, placed at a random angle on a ring of configurable radius (around the tree radius) and a random y that keeps the whole snake inside the segment's height.
- The cobra starts dormant. When the 3D distance between cobra and monkey is below a configured value, it triggers: warning, then fall.
- While falling it drops at a constant vertical speed and also slides around the ring toward the monkey's current angle at a horizontal speed. Both speeds come from `GameBalanceConfig`.
- After falling a configured distance it turns itself off.

## Decisions Already Made

- Placement: random y on the segment, around the tree radius, never outside the segment area.
- Spawn logic: separate component, called by `DTSPool` per segment.
- Trigger: single 3D distance.

## Progress

- Movement 1: done and committed (`e6c4c2a`).
- Movements 2 to 7: still ahead.

## Movements

Kinds: **Claude** = Claude edits and verifies. **User** = user works in the Unity Editor following the steps, Claude then verifies.

### Movement 1: Add Falling Cobra Config to GameBalanceConfig (Claude) — DONE

- Scope: `Assets/_Monkobra/Scripts/GameController/GameBalanceConfig.cs`
- Statement: add a "Falling Cobra" block mirroring Branch: attempt count per segment, spawn probability curve (x ramped difficulty 0~1, y chance 0~1), fall speed (u/s, vertical), horizontal chase speed (u/s along the ring), with getters and `OnValidate` clamps. Existing `GameBalanceConfig.asset` gets the field-initializer defaults.
- End State: Working = project compiles; the asset shows a Falling Cobra block with defaults (attempts 2, curve linear 0 to 0.5, fall 6, horizontal 2).
- Order: none.

### Movement 2: Rework FallingCobra into a Self-Activating Root (Claude)

- Scope: `Assets/_Monkobra/Scripts/MobFallingCobra/FallingCobra.cs`
- Statement: three states, Dormant / Warning / Falling.
  - `Initialize(GameBalanceConfig config, Vector3 treeAxisPosition)` called by the spawner; exposes `LengthU` (snake length plus hit radius) so the spawner can keep it inside the segment.
  - Dormant `Update`: if the 3D distance to the cached monkey is below `triggerDistanceU` (prefab field), start the warning (existing 1.25 s red pulse).
  - Falling `FixedUpdate`: constant downward speed from config; the horizontal step moves the cobra around the ring (constant radius about the tree axis) toward the monkey's current angle by the short arc at the configured horizontal speed.
  - After `maxFallDistanceU` (prefab field) the hit capsule is switched off and the GameObject is set inactive; the segment restart later destroys it. The old fall-speed and despawn-below-player fields go.
  - The hit path is unchanged: capsule tagged `Branch`, enabled only while falling, switched off after the first touch.
- End State: Working = compiles; a cobra dropped into a scene by hand and given an `Initialize` call stays dormant, triggers inside the distance, falls and chases, then turns off at the fall limit.
- Order: Movement 1.

### Movement 3: Rewrite FallingCobraSpawner as a Per-Segment Spawner (Claude)

- Scope: `Assets/_Monkobra/Scripts/MobFallingCobra/FallingCobraSpawner.cs` (rewritten in place, keeps its `.meta` and GUID so the scene component survives)
- Statement: `PopulateSegment(DynamicTreeSegmentPrefabRoot)` rolls the attempt count against the probability curve at the segment's ramped difficulty. For each hit: random angle, `spawnRadiusU` ring, random local y in `[-h/2, h/2 - cobra length]`, spaced at least `minCobraSeparationU` from cobras already on the segment (retry cap, skip on failure). Instantiate as a segment child, call `Initialize`. Fields: cobra prefab, `GameBalanceConfig`, spawn radius, separation, retries. The old global timer, unlock and branch scan are removed.
- End State: Working = compiles; calling `PopulateSegment` on a segment spawns the expected cobras inside the segment bounds, none outside.
- Order: Movements 1, 2.

### Movement 4: Hook the Spawner into DTSPool (Claude)

- Scope: `Assets/_Monkobra/Scripts/Environment/DTSPool.cs`
- Statement: find the `FallingCobraSpawner` the way `webSpawner` is found, call `PopulateSegment` for the first stack in `Start` and for each recycled segment in `TryRecycleLowest`; update the class summary. Null spawner means no cobras, no error.
- End State: Working = compiles; with the spawner present, every segment (first stack and recycled) gets its cobras rolled once per placement.
- Order: Movement 3.

### Movement 5: Wire the Scene in the Unity Editor (User, then Claude verifies)

- Scope: `Assets/_Monkobra/Scenes/Lv1Scene.unity` (the `FallingCobraSpawner` on `DTSPool`), the falling cobra prefab, `Settings/GameBalanceConfig.asset`
- Steps for the user:
  1. Open the project in Unity and let it recompile; confirm the console has no compile errors.
  2. In `Lv1Scene`, select `DTSPool` > `FallingCobraSpawner`: assign the falling cobra prefab and `GameBalanceConfig`.
  3. Press Play once and read the console line `MonkeyPrefabRoot: ready, orbit radius X`; stop Play and set the spawner's spawn radius to about X.
  4. Open the falling cobra prefab and check the new fields (`triggerDistanceU`, `maxFallDistanceU`) show values.
  5. Open `GameBalanceConfig.asset` and check the Falling Cobra block; set a curve that is not 0 at low difficulty if cobras should appear early.
  6. Save the scene and the assets (Ctrl+S) and tell Claude.
- Verification by Claude: read the saved `Lv1Scene.unity` and prefab YAML and confirm the spawner's prefab and config references are non-zero and the radius matches; ask the user to confirm there are no console errors.
- End State: Working = the scene's spawner has every reference assigned, and the user confirms a clean console.
- Order: Movements 1 to 4.

### Movement 6: Playtest and Tune (User, then Claude verifies)

- Scope: no files, unless tuning changes the config values
- Steps for the user:
  1. Play `Lv1Scene` and climb; dormant cobras should hang on the trunk ring, inside their segment.
  2. Approach one: it should pulse red inside the trigger distance, then fall while sliding toward your side of the tree.
  3. Get hit once: shake, stun and drop, one drop per cobra.
  4. Let one miss and fall past the fall limit: it should vanish.
  5. Climb past a couple of recycled segments: no stale cobras and no console errors.
  6. Report anything off (too many, too early, too fast, never triggers) with the numbers you want.
- Verification by Claude: act on the report, adjust values only in the config assets or fields, and re-ask for a go from the user.
- End State: Working = the user confirms behavior and balance.
- Order: Movement 5.

### Movement 7: Update Docs and Changelog (Claude)

- Scope: `docs/mob-doc.md`, `CONTEXT.md`, `docs/monkobra-tdd.md` (only where it mentions the old global spawner), `CHANGELOG.md`
- Statement: rewrite the Falling Cobra "Spawning" and "Behavior" sections and the settings tables (new config home, trigger distance, chase, fall limit, per-segment spawn), fix the script entries, and log the change under `[Unreleased]`.
- End State: Working = no doc still describes the global timer, the 60 u unlock, or the one-at-a-time rule.
- Order: Movement 6.

## Risks and Notes

- A 3D-distance trigger can fire while the cobra is beside or below the monkey, where a fall is useless. Default guard to decide in Movement 2: also require the cobra to be at or above the monkey's height. Say if you do not want it.
- Several cobras can now be live at once, one per qualifying segment.
- Frequency now follows height and the probability curve, not a clock.
- Commit only with Unity closed (AGENTS.md); no commit is planned here.
