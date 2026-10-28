# Monkobra Mobs

Everything in the level that can hurt, hold, or hinder the monkey: the cobra, the falling cobra, tree branches, and spider webs. Design intent is in the [Game Design Document](monkobra-gdd.md), the wider system map in the [Technical Design Document](monkobra-tdd.md), and the banana side of the branch in the [Grab System](grab-doc.md).

## What Happens When the Monkey Touches a Mob

| Mob | Touch | Result for the monkey | Run ends |
| --- | --- | --- | --- |
| Cobra | any solid part of the monkey meets any segment | `GameController.LoseGame()` fires | ✔️ |
| Falling Cobra | any part of the monkey (branch sensor, body, tail, hand) meets its hit capsule | camera shake, stun, drop (same as a branch) | ❌ |
| Branch | the monkey's branch sensor meets a `Branch`-tagged collider | camera shake, stun, drop | ❌ |
| Spider Web | the monkey's solid body meets the web's trigger | body frozen, grab disabled, until the escape bar fills | ❌ |

Stamina is never charged for a mob hit. The cost of a branch, falling cobra, or web is lost height and lost time, which the cobra turns into lost distance.

## How Touches Are Detected

Mobs and the monkey find each other with triggers and tags, never with direct references. Each mob decides for itself which monkey colliders count.

- Branch Sensor: `HitBranchSensor` is a trigger on the monkey prefab, reaching past the body, and reports any collider tagged `Branch`
- Cobra Contact: `CobraSensor` counts the monkey's body and tail (any non-trigger collider under the `Player`-tagged root) and its hands (anything under an `ArmController`), and ignores the monkey's trigger volumes, which reach far past the body
- Web Contact: `SpiderWebTrap` counts only a non-trigger collider whose Rigidbody carries `SpiderWebStruggle`, so a hand brushing a web is ignored
- Tags: `Branch` marks branches and the falling cobra's hit capsule, and `Player` marks the monkey root, see the [conventions](monkobra-tdd.md#conventions)

## Branch

A branch is a prefab (`Prefabs/Environment/BranchWithFruit`) holding a `Branch`-tagged collider and an optional banana.

### Hit Penalty

1. `HitBranchSensor` sees the `Branch` collider and raises `BranchHit` and `Hit` with a `HitKind` (`Branch` or `FallingCobraController`); `CameraHitShakeFeedback` on the main camera hears `Hit` and fires a light camera shake for a branch, a heavier one for a falling cobra
2. `MonkeyController` ignores input for the stun time and drops the monkey a set distance at a set speed, then stops dead on the drop target
3. a second hit during the stun is ignored, so overlapping branches cost one drop, not two
4. the stun fall is involuntary, so it drains no movement stamina

The stun duration and drop distance and speed are in `Settings/GameBalanceConfig.asset`.

### Placement

`TreeSegmentController` decorates each dynamic trunk segment with branches in its `Start`, after `TreeManager` exists. Placement tuning sits in `BranchFruitPlacementConfig`.

- Count: a fixed `branchGenerationAttemptCount` per segment (`GameBalanceConfig`); each attempt samples `branchSpawnProbabilityCurve` at the segment's ramped difficulty (`RampedDifficultyManager`) and spawns a branch if a random roll falls below that chance; why the curve is shaped by difficulty is in [Balance Design](balance-doc.md#one-roll-model)
- Position: a random angle round the trunk and a random height in the segment, at a fixed radius, retried until a spot keeps the minimum separation from branches already placed on that segment, else the last try is accepted
- Banana: `TreeSegmentController` samples `bananaSpawnProbabilityCurve` (`GameBalanceConfig`) at each placed branch's ramped difficulty and passes the chance to `BranchController.Initialize`, which rolls whether the branch bears a banana, then randomizes the stem length and banana rotation

Attempt counts and the branch and banana chance curves are in `Settings/GameBalanceConfig.asset`; branch radius, separation, and placement retries are in `Settings/Mob/BranchFruitPlacementConfig.asset`.

## Cobra

The chasing cobra is code-built geometry on one root: `CobraMotor` poses the body, `CobraSensor` handles contact, and a kinematic Rigidbody lets every segment's trigger report to the root.

### Motion

- Path: the head orbits the trunk axis (`pathCenter`) at a fixed radius while climbing, and the body trails it as a corkscrew
- Never Down: the head only climbs, so a monkey that descends runs into the coil instead of pushing it down
- Closing In: when the coil is within `slowDownDistance` below the monkey, the climb slows to a minimum share of its speed for suspense, but never stops
- Base Speed: outside a surge the cobra climbs at `cobraBaseSpeedCurve` (`GameBalanceConfig`) sampled at the monkey's ramped difficulty, set against the monkey's real average climb (dodging, grabbing, and resting keep it well under its top speed), so a clean run keeps a lead early and the chase tightens late
- Mistake Surge: each mistake of the monkey (a branch or falling cobra hit, counted once per stun, or any web trap) adds `cobraSurgePerMistakeS` of surge time, capped at `cobraMaxSurgeS`, and one stack. While a surge runs the cobra climbs at its base speed plus `cobraSurgeSpeedPerMistakeU` per stack, capped at `cobraMaxSurgeSpeedU`, which may pass the monkey's up speed, and it skips the slow-down near the monkey. When the surge time runs out the stacks reset. The speed eases at `cobraSpeedChangeRateU`; all of these live in `GameBalanceConfig`, wired through `balanceConfig` on the `GreatCobra` prefab's `CobraMotor`. Each stacked mistake raises `CatchUpStarted`: `ProgressBarWidget` shows "The cobra is speeding up!" (with ×N from the 2nd stack) at the screen center, sized to the screen height, fading out, and flashes the cobra marker red while the surge lasts
- Coil: the body spans at least one full loop, and `Awake` clones the last segment until no gap along the coil exceeds `maxSegmentGapU`, so the monkey cannot slip through by orbiting
- Look: a chain of rounded beads with small gaps, slightly tapered, a flat hooded head with primitive-sphere eyes, a raised neck, and a slow slither wave. The hit shape stays gapless even where the visual has gaps

In the starter zone game state the cobra stays active and keeps orbiting, but its height is fixed: it does not climb until the state becomes Playing.

The cobra's shape, orbit, coil, and slow-down values are fields on `CobraMotor`, set on the `GreatCobra` prefab.

The monkey's top climb speed ([Monkey Movement](monkobra-tdd.md#monkey-movement)) is above the cobra's base speed, so it can outrun the cobra, but only by spending stamina.

### Contact

`CobraSensor` runs on trigger enter and on trigger stay, so a monkey already inside the coil when an overlap begins is still caught. Any counted part calls `GameController.LoseGame()`, which runs once per run.

## Falling Cobra

A hazard from above, spawned in `Lv1Scene` as the monkey climbs the endless tree.

### Spawning

`FallingCobraSpawner` sits on the active `DTSPool` object in `Lv1Scene`, never under the pool's mock tree, which the pool deactivates at runtime (it logs a warning if one is left there). Like the web spawner it listens to `TreeSegmentController.BranchesGenerated`, so each roll of a segment's branches gets exactly one roll of falling cobras. Only the first enabled spawner listens.

- Count: a fixed `fallingCobraAttemptCount` per segment (`GameBalanceConfig`); each attempt samples `fallingCobraSpawnProbabilityCurve` at the segment's ramped difficulty (`RampedDifficultyManager`, read at the segment's middle), the same model as branches
- Spot: a cobra hangs on a ring of `spawnRadiusU` about the segment's pivot, on the trunk axis, at a random angle and a random height that keeps the whole snake inside its segment. A spot too close to another cobra of the segment is retried, then skipped
- Banana Ambush: with `fallingCobraBananaAmbushChance` (`GameBalanceConfig`) a cobra instead hangs at the angle of one banana of its segment, `ambushMinAboveBananaU` to `ambushMaxAboveBananaU` above it (on the spawner); with no banana, or no room above it inside the segment, it falls back to a random spot
- Cleanup: cobras are children of their segment, so `Restart` clears them with the branches. Before rolling a segment again the spawner drops that segment's previous cobras too
- Concurrency: several can be live at once, one roll per segment; frequency follows climb height and the curve, not a clock

Fall and chase speeds, the wake distance, and the ambush chance are in `Settings/GameBalanceConfig.asset`; spawn radius, separation, and ambush height are on `FallingCobraSpawner` on `DTSPool`; trigger and fall distances are on the falling cobra prefab.

### Behavior

- Dormant: it hangs still, in its normal color, until the monkey is within the trigger distance (3D), or within the farther reach wake distance while either arm reaches for a banana, and the cobra is at or above the monkey's height, so a grab is the riskiest moment to stand under a cobra
- Warning: it then pulses red against its green base for a short warning and has no hit shape
- Fall: it clears the warning color, then drops at a constant speed with one capsule trigger tagged `Branch` covering the whole snake, while every segment collider is switched off. Each physics step it re-reads the monkey's angle about the tree and slides round the trunk toward it by the shortest arc at the horizontal speed, never overshooting, so it keeps tracking a monkey that orbits
- Hit: any part of the snake touching any part of the monkey applies the heavy shake, stun, and drop. The capsule is tagged `Branch`, so the monkey's branch sensor handles its own contact; a contact on the body, tail, or a hand calls `HitBranchSensor.ReportHit` for the same drop. After a hit the snake stops homing in and its hit capsule rests for the stun plus `fallingCobraRehitGraceS`, so the monkey can dodge sideways; a monkey still inside once it returns is dropped again, up to `fallingCobraMaxHits` drops per snake
- Off: after falling the max fall distance it switches itself off; the segment's restart destroys it later

## Spider Web

A static trap on the trunk that holds the monkey in place while the cobra keeps climbing.

### Placement

`SpiderWebSpawner` sits on `Envs/DTSPool` in `Lv1Scene`, and on any object that stays active from scene load, never under the pool's mock tree, which `DTSPool` deactivates at runtime (it logs a warning if one is left there). It listens to `TreeSegmentController.BranchesGenerated`, raised each time a segment has rolled its branches, on its first `Start` and after every `DTSPool` restart, so each roll of branches gets exactly one roll of webs. Only the first enabled spawner listens.

- Count: a fixed `webAttemptCount` per segment (`GameBalanceConfig`); each attempt samples `webSpawnProbabilityCurve` at the segment's ramped difficulty (`RampedDifficultyManager`, read at the segment's middle), the same model as branches
- Spot: a web lies flat on its segment's bark facing outward. A spot is dropped if it is too close to another live web, or its trigger volume (plus clearance) overlaps a branch or fruit. After the retry cap the web is skipped rather than placed overlapping
- Cleanup: webs are children of their segment, so `Restart` clears them with the branches. Before rolling a segment again the spawner drops that segment's previous webs too, destroying any still active, so none linger and none double up. A web removed under a held monkey frees it

The attempt count and spawn probability curve are in `Settings/GameBalanceConfig.asset`; the minimum separation is on `SpiderWebSpawner` on `Envs/DTSPool`.

The curve is the only gate: webs appear wherever it is above 0, and the ramped difficulty is 0 until the climb reaches the start of `rampedDifficultyCurve`.

### Trap and Escape

1. Catch: `SpiderWebTrap` hands itself to the monkey's `SpiderWebStruggle` on trigger enter or stay. The struggle refuses if the monkey is already trapped, still protected, the game is halted, or the web is missing
2. Held: `MonkeyController.IsHeld` freezes the body, which ignores input and any branch-hit fall, but is kept awake so the cobra's trigger still registers it. The body is also pulled in 3D to the web's hub (`PullTo`, `pullSpeedU` on `MonkeyController`); on escape it glides back onto its orbit ring at the height it was pulled to The interact action is disabled, which cancels a reach in progress and swings the camera back behind
3. Struggle: each press of the escape action (Jump, Space) adds progress and pulses the web (squash and twist). Progress drains while idle
4. Break Free: a full bar removes the web, unfreezes the body, and grants brief immunity to every web. Interact stays off until that immunity ends, so leftover presses do not fire a grab
5. Prompt: `SpiderWebWidget` shows a hint on the first trap per scene load and the escape bar on every trap, and hides both on escape or game over. A low-stamina hint on screen at the time steps aside until the escape ([UI](monkobra-tdd.md#ui))

The escape progress per press, its decay while idle, and the immunity after escaping are fields on `SpiderWebStruggle`.

The struggle suspends interact because of a shared Space binding, see [Input](monkobra-tdd.md#input), and freezes while the game is paused or over.

### Testing

Webs are in `Lv1Scene` and follow the web probability curve. For a quick test select `Envs/DTSPool`, tick `SpiderWebSpawner` > Use Test Override before pressing Play, and leave the scene unsaved. Webs then spawn at the Test Spawn Chance per attempt at any height, ignoring the curve, and may land on the monkey's start spot. Ticked during Play, it only reaches segments rolled after that, the ones the pool restarts above the first stack. To tune the real spawn rate, edit the Spider Web group of `Settings/GameBalanceConfig.asset` instead. Gizmos show each web's trigger as a yellow box.

## Known Gaps

- Mob tuning for the cobra, the web escape, and web geometry lives on scene components, not in a shared ScriptableObject; only the web and falling cobra spawn rates, speeds, and attempt counts are in `GameBalanceConfig`
- Stamina is not linked to branch hits
- `Lv1Scene`'s Canvas holds 2 objects named `ProgressBar`, the climb progress and the web escape bar, so look them up by path
