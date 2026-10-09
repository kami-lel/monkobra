# Monkobra Mobs

Everything in the level that can hurt, hold, or hinder the monkey: the cobra, the falling cobra, tree branches, and spider webs. Design intent is in the [Game Design Document](monkobra-gdd.md), the wider system map in the [Technical Design Document](monkobra-tdd.md), and the banana side of the branch in the [Grab System](grab-doc.md).

## What Happens When the Monkey Touches a Mob

| Mob | Touch | Result for the monkey | Run ends |
| --- | --- | --- | --- |
| Cobra | any solid part of the monkey meets any segment | `GameController.LoseGame` fires | ✔️ |
| Falling Cobra | the monkey's branch sensor meets its hit capsule | camera shake, stun, drop (same as a branch) | ❌ |
| Branch | the monkey's branch sensor meets a `Branch`-tagged collider | camera shake, stun, drop | ❌ |
| Spider Web | the monkey's solid body meets the web's trigger | body frozen, grab disabled, until the escape bar fills | ❌ |

Stamina is never charged for a mob hit. The cost of a branch, falling cobra, or web is lost height and lost time, which the cobra turns into lost distance.

## How Touches Are Detected

Mobs and the monkey find each other with triggers and tags, never with direct references. Each mob decides for itself which monkey colliders count.

- Branch Sensor: `HitBranchDetection` is a trigger on the monkey prefab, reaching past the body, and reports any collider tagged `Branch`
- Cobra Contact: `CobraCollisions` counts the monkey's body and tail (any non-trigger collider under the `Player`-tagged root) and its hands (anything under an `ArmRoot`), and ignores the monkey's trigger volumes, which reach far past the body
- Web Contact: `SpiderWebTrap` counts only a non-trigger collider whose Rigidbody carries `SpiderWebStruggle`, so a hand brushing a web is ignored
- Tags: `Branch` marks branches and the falling cobra's hit capsule, and `Player` marks the monkey root, see the [conventions](monkobra-tdd.md#conventions)

## Branch

A branch is a prefab (`Prefabs/Environment/BranchWithFruit`) holding a `Branch`-tagged collider and an optional banana.

### Hit Penalty

1. `HitBranchDetection` sees the `Branch` collider, fires a Cinemachine impulse (camera shake), and raises `BranchHit`
2. `MonkeyPrefabRoot` ignores input for the stun time and drops the monkey a set distance at a set speed, then stops dead on the drop target
3. a second hit during the stun is ignored, so overlapping branches cost one drop, not two
4. the stun fall is involuntary, so it drains no movement stamina

| Setting | Value | Asset |
| --- | --- | --- |
| Stun Duration | 0.75 s | `Settings/MonkeyConfig.asset` |
| Drop Distance | 10 u | 〃 |
| Drop Speed | 8 u/s | 〃 |

### Placement

`DynamicTreeSegmentPrefabRoot` decorates each dynamic trunk segment with branches in its `Start`, after `TreeManager` exists. Placement tuning sits in `BranchFruitPlacementConfig`.

- Count: a fixed `branchGenerationAttemptCount` per segment (`GameBalanceConfig`); each attempt samples `branchSpawnProbabilityCurve` at the segment's ramped difficulty (`RampedDifficultyService`, linear 0 to 1 by default) and spawns a branch if a random roll falls below that chance, so the tree gets denser with height
- Position: a random angle round the trunk and a random height in the segment, at a fixed radius, retried until a spot keeps the minimum separation from branches already placed on that segment, else the last try is accepted
- Banana: `DynamicTreeSegmentPrefabRoot` samples `bananaSpawnProbabilityCurve` (`GameBalanceConfig`, linear 0 to 1 by default) at each placed branch's ramped difficulty and passes the chance to `BranchWithScriptRoot.Initialize`, which rolls whether the branch bears a banana, then randomizes the stem length and banana rotation

| Setting | Value | Asset |
| --- | --- | --- |
| Branches At Tree Base | 2 | `Settings/BranchFruitPlacementConfig.asset` |
| Branches At Max Density | 9 | 〃 |
| Max Density Reached At | 60% of the climb | 〃 |
| Branch Radius | 6.6 u | 〃 |
| Min Separation | 3 u | 〃 |
| Placement Retries | 30 | 〃 |
| Banana Chance | 0.15 | `Prefabs/Environment/BranchWithFruit` |

## Cobra

The chasing cobra is code-built geometry on one root: `CobraClimb` poses the body, `CobraCollisions` handles contact, and a kinematic Rigidbody lets every segment's trigger report to the root.

### Motion

- Path: the head orbits the trunk axis (`pathCenter`) at a fixed radius while climbing, and the body trails it as a corkscrew
- Never Down: the head only climbs, so a monkey that descends runs into the coil instead of pushing it down
- Closing In: when the coil is within `slowDownDistance` below the monkey, the climb slows to a minimum share of its speed for suspense, but never stops
- Coil: the body spans at least one full loop, and `Awake` clones the last segment until no gap along the coil exceeds `maxSegmentGapU`, so the monkey cannot slip through by orbiting
- Look: a chain of rounded beads with small gaps, slightly tapered, a flat hooded head with primitive-sphere eyes, a raised neck, and a slow slither wave. The hit shape stays gapless even where the visual has gaps

| Setting (`Lv1Scene`) | Value |
| --- | --- |
| Orbit Radius | 5.5 u |
| Start Height | -5 u |
| Climb Speed | 3 u/s |
| Orbit Speed | 45 deg/s, clockwise |
| Coil Turns | 1.1 |
| Coil Pitch | 1.5 u per loop |
| Max Segment Gap | 0.4 u |
| Slow Down Distance | 2 u |
| Min Speed Share Near Player | 0.3 |

For scale, the monkey climbs at up to 6 u/s ([Monkey Movement](monkobra-tdd.md#monkey-movement)), so it can outrun the cobra, but only by spending stamina.

### Contact

`CobraCollisions` runs on trigger enter and on trigger stay, so a monkey already inside the coil when an overlap begins is still caught. Any counted part calls `GameController.LoseGame`, which runs once per run.

## Falling Cobra

A hazard from above, spawned in `Lv1Scene` as the monkey climbs the endless tree.

### Spawning

`FallingCobraSpawner` is attached to the active `DTSPool` object in `Lv1Scene`. It finds the `Player`-tagged monkey, uses the pool's transform as the tree center, and tracks the highest climb since the start. It does not depend on the mock tree, which the pool deactivates at runtime.

1. Unlock: spawning begins once the monkey has climbed `startClimbHeightU` above its starting height, and stays unlocked even if the monkey later falls
2. Spot: it looks for a `Branch`-tagged object 4 to 9 u above the monkey whose angle round the trunk puts it within 3 u (sideways) of the monkey, nearest first, and spawns the cobra on the monkey's own orbit ring above that branch. If none qualifies it retries each second
3. Rhythm: only one falling cobra exists at a time. The wait before the next one shortens from the initial interval to the minimum as the peak climb after unlock grows

| Setting (`Lv1Scene` > `DTSPool` > `FallingCobraSpawner`) | Value |
| --- | --- |
| Unlock Climb | 60 u |
| Initial Interval | 8 s |
| Min Interval | 4 s |
| Climb To Reach Min Interval | 200 u after unlock |
| Branch Window Above Player | 4 to 9 u |
| Max Side Distance | 3 u |

### Behavior

- Warning: it starts red immediately, then pulses red against its green base for 1.25 s at 4 Hz and has no hit shape
- Fall: it clears the warning color, returns to green, then drops at 6 u/s with one capsule trigger tagged `Branch` covering the whole snake, while every segment collider is switched off
- Hit: because the capsule is tagged `Branch`, the monkey's own branch sensor applies the usual shake, stun, and drop, and the capsule is switched off after the first touch so one snake costs one drop
- Despawn: 5 u below the monkey it destroys itself, which frees the spawner for the next one

## Spider Web

A static trap on the trunk that holds the monkey in place while the cobra keeps climbing.

### Placement

`SpiderWebSpawner` sits on the tree root and runs after the default execution order, so every segment already has its branches.

- Slots: each dynamic segment rolls a fixed number of slots, each with a spawn chance, only above `startProgress` of the climb
- Spot: a web lies flat on its segment's bark facing outward. A spot is dropped if it is below the start height, too close to another web, or its trigger volume (plus clearance) overlaps a branch or fruit. After the retry cap the web is skipped rather than placed overlapping
- Runtime Segments: the pass covers segments present at load, so a runtime generator must call `PopulateSegment`; `DTSPool` does so after each segment restart

| Setting (`Lv1Scene`) | Value |
| --- | --- |
| Start Progress | 0.5 (upper half) |
| Slots Per Segment | 2 |
| Spawn Chance Per Slot | 0.5 |
| Min Web Separation | 4 u |

### Trap and Escape

1. Catch: `SpiderWebTrap` hands itself to the monkey's `SpiderWebStruggle` on trigger enter or stay. The struggle refuses if the monkey is already trapped, still protected, the game is halted, or the web is missing
2. Held: `MonkeyPrefabRoot.IsHeld` freezes the body, which ignores input and any branch-hit fall, but is kept awake so the cobra's trigger still registers it. The interact action is disabled, which cancels a reach in progress and swings the camera back behind
3. Struggle: each press of the escape action (Jump, Space) adds progress and pulses the web (squash and twist). Progress drains while idle
4. Break Free: a full bar removes the web, unfreezes the body, and grants brief immunity to every web. Interact stays off until that immunity ends, so leftover presses do not fire a grab
5. Prompt: `SpiderWebPrompt` shows a hint on the first trap per scene load and the escape bar on every trap, and hides both on escape or game over

| Setting | Value |
| --- | --- |
| Progress Per Press | 0.12 (9 presses from empty) |
| Decay While Idle | 0.35 per s |
| Immunity After Escape | 1 s |

The struggle suspends interact because of a shared Space binding, see [Input](monkobra-tdd.md#input), and freezes while the game is paused or over.

### Testing

Webs are in `Lv1Scene`, and `WebDemo` is a smaller test scene with the same setup and no score. Webs spawn only from the middle of the tree up, so for a quick test set `SpiderWebSpawner` > Start Progress to `0` and Spawn Chance to `1` on the `Tree` object, and leave the scene unsaved. Gizmos show each web's trigger as a yellow box and the start height as a ring.

## Known Gaps

- Mob tuning for the cobra and webs lives on scene components, not in a shared ScriptableObject
- Stamina is not linked to branch hits
- `Lv1Scene`'s Canvas holds 2 objects named `ProgressBar`, the climb progress and the web escape bar, so look them up by path
