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

### During a Pineapple Leap

While `GameController.IsLeaping` is set, from the grab to the end of the landing grace ([Grab System](grab-doc.md#pineapple-leap)), no mob touches the monkey. Each checks the flag itself, before any of its effects:

- Branch: `HitBranchSensor` reports no hit, so no stun, shake, surge, or analytics stamp
- Falling Cobra: `TryHit` returns without spending the snake's hit, and a dormant snake does not wake
- Spider Web: `SpiderWebTrap` neither catches nor raises `Encountered`
- Cobra: `CobraSensor` ignores contact

Rough bark refuses a slowdown for the same span. The arms have collisions off while the hands are up and the body is pulled, so they catch on nothing.

## How Touches Are Detected

Mobs and the monkey find each other with triggers and tags, never with direct references. Each mob decides for itself which monkey colliders count.

- Branch Sensor: `HitBranchSensor` is a trigger on the monkey prefab, reaching past the body, and reports any collider tagged `Branch`
- Cobra Contact: `CobraSensor` counts the monkey's body and tail (any non-trigger collider under the `Player`-tagged root) and its hands (anything under an `ArmController`), and ignores the monkey's trigger volumes, which reach far past the body
- Web Contact: `SpiderWebTrap` counts only a non-trigger collider whose Rigidbody carries `SpiderWebStruggle`, so a hand brushing a web is ignored
- Tags: `Branch` marks branches and the falling cobra's hit capsule, and `Player` marks the monkey root, see the [conventions](monkobra-tdd.md#conventions)

## Branch

A branch is one of 4 kinds, each its own prefab and a variant of `Prefabs/Environment/BranchWithFruit`: `BranchEmpty`, `BranchBanana`, `BranchCoconut` and `BranchPineapple`. Each holds a `Branch`-tagged collider and the fruit of its kind, if any (`BranchKind`).

### Hit Penalty

1. `HitBranchSensor` sees the `Branch` collider and raises `BranchHit` and `Hit` with a `HitKind` (`Branch` or `FallingCobraController`); `CameraHitShakeFeedback` on the main camera hears `Hit` and fires a light camera shake for a branch, a heavier one for a falling cobra, scaled by `CameraConfig`
2. `MonkeyController` ignores input for the stun time and drops the monkey a set distance at a set speed, then stops dead on the drop target
3. a second hit during the stun is ignored, so overlapping branches cost one drop, not two
4. the stun fall is involuntary, so it drains no movement stamina

The stun duration and drop distance and speed are in `Settings/Player/PlayerBalanceConfig.asset`.

### Placement

`TreeSegmentController` decorates each dynamic trunk segment with branches in its `Start`, after `TreeManager` exists. Placement tuning sits in `BranchFruitPlacementConfig`.

- Count: each of the 4 kinds has its own fixed attempt count per segment (`<kind>BranchAttemptCount`, `WorldBalanceConfig`); each attempt samples that kind's own `<kind>BranchProbabilityCurve` at the segment's ramped difficulty (`RampedDifficultyManager`) and grows a branch of that kind if a random roll falls below that chance; kinds roll rarest first (pineapple, coconut, banana, empty) into one shared set of placed spots, so a common kind never crowds a rare one out; why the curves are shaped by difficulty is in [Balance Design](balance-doc.md#one-roll-model)
- Position: a random angle round the trunk and a random height in the segment, at a fixed radius, retried until a spot keeps the minimum separation from branches already placed on that segment, else the last try is accepted
- Starting Zone Chance: a segment whose middle lies below the exit trigger grows only banana branches, each attempt at the single `startingZoneBananaBranchChance` (`WorldBalanceConfig`), instead of the curves; the empty, coconut and pineapple kinds do not grow there
- Monkey Clearance: before the run is playing (tutorial screen and starting zone) a branch spot within `startingZoneBranchClearRadiusU` (`WorldBalanceConfig`, 0 turns it off) of the monkey is rejected and retried; if no clear spot is found the branch is skipped, so a restart never spawns the monkey into a branch. The monkey is read from `RampedDifficultyManager.PlayerTransform`
- Fruit: the fruit is decided by the prefab, not by a roll on the branch: `BranchController.Setup` shows the fruit its `BranchKind` names (the banana child, or a coconut or pineapple spawned from its `fruitPrefab` on the stem, the banana child then off) or none for an empty branch, and randomizes the stem length and fruit rotation

Attempt counts and the branch and banana chance curves are in `Settings/Environment/WorldBalanceConfig.asset`; branch radius, separation, and placement retries are in `Settings/Environment/BranchFruitPlacementConfig.asset`.

## Cobra

The chasing cobra is code-built geometry on one root: `CobraMotor` poses the body, `CobraSensor` handles contact, and a kinematic Rigidbody lets every segment's trigger report to the root.

### Motion

- Path: the head orbits the trunk axis (`pathCenter`) at a fixed radius while climbing, and the body trails it as a corkscrew
- Never Down: the head only climbs, so a monkey that descends runs into the coil instead of pushing it down
- Base Speed: outside a surge the cobra climbs at the monkey's top climb speed (`MonkeyClimbConfig`, wired as `monkeyClimbConfig` on the `CobraMotor`) times `cobraBaseSpeedCurve` (`CobraBalanceConfig`) sampled at the monkey's ramped difficulty, a multiplier from 0 to 2 where 1 is that top speed. It is set against the monkey's real average climb (dodging, grabbing, and resting keep it well under its top speed), so a clean run keeps a lead early and the chase tightens late
- Distance Scale: that multiplier is multiplied again by `cobraDistanceSpeedCurve` (`CobraBalanceConfig`), read at the vertical gap from the coil top up to the monkey (never below 0), so a cobra far behind runs faster and one close behind runs slower, but it never stops. The surge and the exit boost add on top of the final speed
- Zone Exit Boost: when the monkey leaves the starting zone (state becomes Playing) the cobra climbs at its base speed plus `cobraExitBoostSpeedU` for `cobraExitBoostDurationS` (`CobraBalanceConfig`; speed 0 turns it off), eased by `cobraSpeedChangeRateU` like any speed change. A running mistake surge takes precedence over it. `CobraMotor` also raises `ChaseStarted` once, and `ProgressBarWidget` shows `chaseStartText` ("Run! The Great Cobra is chasing you!") with the same center cue as the surge warning
- Mistake Surge: each mistake of the monkey outside the starting zone (a branch or falling cobra hit, counted once per stun, or any web trap; in the zone the cobra holds still, so a mistake there adds nothing and shows no cue) adds `cobraSurgePerMistakeS` of surge time, capped at `cobraMaxSurgeS`, and one stack. While a surge runs the cobra climbs at its base speed plus `cobraSurgeSpeedPerMistakeU` per stack, each stack scaled by its share on `cobraSurgeStackShareCurve` (x: stack number, y: share, so later mistakes can add less), capped at `cobraMaxSurgeSpeedU`, which may pass the monkey's up speed. When the surge time runs out the stacks reset. The speed eases at `cobraSpeedChangeRateU`; all of these live in `CobraBalanceConfig`, wired through `cobraBalanceConfig` on the `GreatCobra` prefab's `CobraMotor`. Each stacked mistake raises `CatchUpStarted`: `ProgressBarWidget` shows "The cobra is speeding up!" (with ×N from the 2nd stack) at the screen center, sized to the screen height, fading out, and flashes the cobra marker red while the surge lasts
- Coil: the body spans at least one full loop, and `Awake` clones the last segment until no gap along the coil exceeds `maxSegmentGapU`, so the monkey cannot slip through by orbiting
- Look: a chain of rounded beads with small gaps, slightly tapered, a flat hooded head with primitive-sphere eyes, a raised neck, and a slow slither wave. The hit shape stays gapless even where the visual has gaps

In the starting zone game state the cobra stays active and keeps orbiting, but its height is fixed: it does not climb until the state becomes Playing.

The cobra's shape, orbit, coil, and slow-down values are fields on `CobraMotor`, set on the `GreatCobra` prefab.

The monkey's top climb speed ([Monkey Movement](monkobra-tdd.md#monkey-movement)) is above the cobra's base speed, so it can outrun the cobra, but only by spending stamina.

### Camera Alert

`CameraRigManager` cuts to the cobra alert shot only once, on the cobra's `ChaseStarted`, the moment it first starts moving after the monkey leaves the starting zone, then hands back. No other cobra event moves the camera. The shot shows the whole cobra: `CameraCobraAlertFraming` stands the camera close, a set horizontal distance out from the monkey on the anchor's yaw, so it only travels up and down, aims at the middle of the cobra's own coil, wherever it lies below the monkey, from a height that looks down by a set angle, lifted by a y offset so the cobra sits lower in the view, and widens the field of view to fit it, up to a limit. It stays quiet while the cobra guards the boss arena. The shot's priority, cap, and cooldown, and the fit margin, widest field of view, and smoothing, are in `CameraConfig`.

### Rough Bark Shake

While the monkey moves across rough bark (`MonkeyController.RoughBarkSpeedScale` below 1, input held, not held by a web) `CameraRoughBarkShake` eases the Perlin noise of every camera up, and back down when the movement or the contact stops. Gains and ease times are in `CameraConfig`.

### Coconut Freeze

`CobraMotor.TryFreeze`, called by `CoconutManager` when a thrown coconut lands, on the cobra or after its fixed fall ([Grab System](grab-doc.md#coconut)), holds the whole cobra still for `cobraFreezeDurationS` (`PlayerBalanceConfig`): no climb, no orbit, no slither wave.

- Start: the running mistake surge and the zone exit boost are cleared and the climb speed drops to 0, so after the freeze the cobra eases back up at `cobraSpeedChangeRateU`
- During: mistakes add no surge and raise no `CatchUpStarted`, so no warning shows; the base speed and the slow-down near the monkey resume at the thaw
- Refused: a freeze while one runs is refused and spends nothing, so freezes never stack or reset
- Contact: unchanged, a frozen coil is as fatal as a moving one
- Look: `CobraFreezeTint` beside `CobraMotor` tints the body light blue, blinking back in the last seconds, and leaves the eyes black; the coconut counter shows the countdown
- Events: `Frozen` (with the length) and `Thawed`; falling cobras are never frozen

### Contact

`CobraSensor` runs on trigger enter and on trigger stay, so a monkey already inside the coil when an overlap begins is still caught. Any counted part calls `GameController.LoseGame()`, which runs once per run, also while a coconut freezes the cobra.

## Falling Cobra

A hazard from above, spawned in `FullPlayScene` as the monkey climbs the endless tree.

### Spawning

`FallingCobraSpawner` sits on the active `DynamicTreeSegmentPool` object in `FullPlayScene`, never under the pool's mock tree, which the pool deactivates at runtime (it logs a warning if one is left there). Like the web spawner it listens to `TreeSegmentController.BranchesGenerated`, so each roll of a segment's branches gets exactly one roll of falling cobras. Only the first enabled spawner listens.

- Count: a fixed `fallingCobraAttemptCount` per segment (`CobraBalanceConfig`); each attempt samples `fallingCobraSpawnProbabilityCurve` at the segment's ramped difficulty (`RampedDifficultyManager`, read at the segment's middle), the same model as branches
- Spot: a cobra hangs on a ring of `spawnRadiusU` about the segment's pivot, on the trunk axis, at a random angle and a random height that keeps the whole snake inside its segment. A spot too close to another cobra of the segment is retried, then skipped
- Banana Ambush: with `fallingCobraBananaAmbushChance` (`CobraBalanceConfig`) a cobra instead hangs at the angle of one banana of its segment, `ambushMinAboveBananaU` to `ambushMaxAboveBananaU` above it (on the spawner); with no banana, or no room above it inside the segment, it falls back to a random spot. The pick goes by the `FruitCollider` tag, so a pineapple or a coconut can be ambushed too
- Cleanup: cobras are children of their segment, so `Restart` clears them with the branches. Before rolling a segment again the spawner drops that segment's previous cobras too
- Concurrency: several can be live at once, one roll per segment; frequency follows climb height and the curve, not a clock

Fall and chase speeds, the wake distance, and the ambush chance are in `Settings/MobGreatCobra/CobraBalanceConfig.asset`; spawn radius, separation, and ambush height are on `FallingCobraSpawner` on `DynamicTreeSegmentPool`; trigger and fall distances are on the falling cobra prefab.

### Behavior

- Dormant: it hangs still, in its normal color, until the monkey is within the trigger distance (3D), or within the farther reach wake distance while either arm reaches for a banana, and the cobra is at or above the monkey's height, so a grab is the riskiest moment to stand under a cobra
- Warning: it then pulses red against its green base for a short warning and has no hit shape
- Fall: it clears the warning color, then drops at a constant speed with one capsule trigger tagged `Branch` covering the whole snake, while every segment collider is switched off. Each physics step it re-reads the monkey's angle about the tree and slides round the trunk toward it by the shortest arc at the horizontal speed, never overshooting, so it keeps tracking a monkey that orbits
- Branch Block: the drop and the slide are each swept as the hit capsule (`CapsuleCast`, triggers ignored) and stop a small skin short of any solid collider under a `BranchController`, so a branch holds the snake up and a sideways slide stops against it; it keeps homing, so it may slip off the branch edge and fall on. Only real drop counts toward the max fall distance, a branch already overlapping the snake at the start never blocks it, and other falling cobras, webs and fruit triggers never do
- Hit: any part of the snake touching any part of the monkey applies the heavy shake, stun, and drop. The capsule is tagged `Branch`, so the monkey's branch sensor handles its own contact; a contact on the body, tail, or a hand calls `HitBranchSensor.ReportHit` for the same drop. After a hit the snake stops homing in and its hit capsule rests for the stun plus `fallingCobraRehitGraceS`, so the monkey can dodge sideways; a monkey still inside once it returns is dropped again, up to `fallingCobraMaxHits` drops per snake
- Off: after falling the max fall distance it switches itself off; the segment's restart destroys it later

## Spider Web

A static trap on the trunk that holds the monkey in place while the cobra keeps climbing.

### Placement

`SpiderWebSpawner` sits on `Envs/DynamicTreeSegmentPool` in `FullPlayScene`, and on any object that stays active from scene load, never under the pool's mock tree, which `DynamicTreeSegmentPool` deactivates at runtime (it logs a warning if one is left there). It listens to `TreeSegmentController.BranchesGenerated`, raised each time a segment has rolled its branches, on its first `Start` and after every `DynamicTreeSegmentPool` restart, so each roll of branches gets exactly one roll of webs. Only the first enabled spawner listens.

- Count: a fixed `webAttemptCount` per segment (`WorldBalanceConfig`); each attempt samples `webSpawnProbabilityCurve` at the segment's ramped difficulty (`RampedDifficultyManager`, read at the segment's middle), the same model as branches
- Spot: a web lies flat on its segment's bark facing outward. A spot is dropped if it is too close to another live web, or its trigger volume (plus clearance) overlaps a branch or fruit. After the retry cap the web is skipped rather than placed overlapping
- Cleanup: webs are children of their segment, so `Restart` clears them with the branches. Before rolling a segment again the spawner drops that segment's previous webs too, destroying any still active, so none linger and none double up. A web removed under a held monkey frees it

The attempt count and spawn probability curve are in `Settings/Environment/WorldBalanceConfig.asset`; the minimum separation is on `SpiderWebSpawner` on `Envs/DynamicTreeSegmentPool`.

The curve is the only gate: webs appear wherever it is above 0, and the curve sits at 0 until the difficulty of the web's first appearance height.

### Trap and Escape

1. Catch: `SpiderWebTrap` hands itself to the monkey's `SpiderWebStruggle` on trigger enter or stay. The struggle refuses if the monkey is already trapped, still protected, the game is halted, or the web is missing
2. Held: `MonkeyController.IsHeld` freezes the body, which ignores input and any branch-hit fall, but is kept awake so the cobra's trigger still registers it. The body is also pulled in 3D to the web's hub (`PullTo`, `pullSpeedU` on `MonkeyController`); on escape it glides back onto its orbit ring at the height it was pulled to The interact action is disabled, which cancels a reach in progress and swings the camera back behind
3. Struggle: each press of the escape action (Jump, Space) adds progress and pulses the web (squash and twist). Progress drains while idle
4. Break Free: a full bar removes the web, unfreezes the body, and grants brief immunity to every web. Interact stays off until that immunity ends, so leftover presses do not fire a grab
5. Prompt: `SpiderWebWidget` asks `PopUpManager` for a hint on the first trap per scene load and shows the escape bar on every trap, and hides both on escape or game over. A low-stamina hint on screen at the time steps aside until the escape ([UI](monkobra-tdd.md#ui))

The escape progress per press, its decay while idle, and the immunity after escaping are fields on `SpiderWebStruggle`.

The struggle suspends interact because of a shared Space binding, see [Input](monkobra-tdd.md#input), and freezes while the game is paused or over.

### Testing

Webs are in `FullPlayScene` and follow the web probability curve. For a quick test select `Envs/DynamicTreeSegmentPool`, tick `SpiderWebSpawner` > Use Test Override before pressing Play, and leave the scene unsaved. Webs then spawn at the Test Spawn Chance per attempt at any height, ignoring the curve, and may land on the monkey's start spot. Ticked during Play, it only reaches segments rolled after that, the ones the pool restarts above the first stack. To tune the real spawn rate, edit the Spider Web group of `Settings/Environment/WorldBalanceConfig.asset` instead. Gizmos show each web's trigger as a yellow box.

## Cobra King (Boss)

The `Boss/BossFight` prefab sits at the root of `FullPlayScene`; everything in the arena is built at runtime around the trunk.

- Trigger: once the monkey has climbed `bossTriggerHeightU` (`WorldBalanceConfig`, the number on the trunk height markers, inside the boss zone between `bossZoneStartU` and `bossZoneEndU`) in Playing, the screen flashes white and the run enters the boss arena. The zone sits well past the basic run's whole cast, so a player has met and practised each hazard first: webs, pineapples, falling cobras, coconuts and rough bark, each first appearing where its own curve leaves zero; lower the 3 heights in the asset to test
- Bare Zone: from `bossZoneStartU` to `bossZoneEndU` nothing but the fight is generated: branches of every kind, webs, falling cobras and rough bark each skip any spot whose own height falls in the zone (`RampedDifficultyManager.IsInBossZoneY`), so the trunk is bare there and the arena's own branches only ever bear bananas; the ramp holds its top value through the zone and ordinary generation resumes above the zone end
- Height Marker: the trunk height marker at `bossTriggerHeightU` keeps its number but sits on a bigger, red, beating plate (`HeightMarkerSystem`, Boss Marker fields) with a small look-alike of the Cobra King lying on top (`BossKingBody.BuildFigure`, `markerFigureScale`), so the player sees the fight coming; once the fight begins that marker is no longer shown
- Arena: the king coils round the trunk `nestHeightU` above the entry, its neck and head hanging toward the monkey; the zone is already bare, so only branches of a segment reaching past the zone end are cleared near the nest, and segments recycled during the fight lose their branches near the nest
- Arena Branches: the bare zone grows no tree branches, but the fight was tuned w/ the climb's own around it, so it grows the same itself once it begins (`SpawnArenaBranches`): from the guard up to the nest, `arenaBranchAttemptsPer10U` attempts per 10 u each growing a branch at `arenaBranchChance` (the climb's branch curve there b4 the zone), `arenaBranchSeparationU` apart, none within `arenaBranchStartClearU` of the monkey's start; `arenaBranchFruitChance` of them bear a fruit, shown as a watermelon and thrown like any arena fruit, but never growing back. They stay through a restart
- Great Cobra: stops chasing and guards `guardDepthU` below the entry without climbing, still circling the trunk (`CobraMotor.StartGuard`); every `peekIntervalRangeS` the first `peekNeckU` of its neck lifts off the coil in a smooth curve, bowing `peekCurlU` out from the trunk, and the head springs `peekRiseU` up with a small overshoot and a gentle sway, the coil staying put; contact stays fatal, so only a monkey lingering that low is at risk. A monkey more than `retreatLimitU` below the entry hears "Nowhere to run!"
- Pineapple Leap: a leap never carries the monkey past the arena: `PineappleLeapController` caps its pull at `BossFightController.LeapTopY`, the fight's start height before the fight and the ceiling under the king during it, so a pineapple grabbed just below the arena lands right at its start
- Keep Away: the monkey can never reach the king: `MonkeyController.SetHeightCeiling` caps its climb `ceilingGapU` below the king's chin however long up is held, and a monkey within `pushRangeU` of the cap gets butted down
- King: built at runtime from Unity primitives, kept simple (`BossKingBody`): a round bead coil, neck, a big round head with two eyes and a small gold crown; thrown bananas fly at an invisible target on the crown's front
- Camera: in the arena `CameraClimbFraming.SetBossFraming` pulls the climb camera back to `cameraDistanceScale` times its distance and tilts it up by `cameraFocusShare` of the height to the king's head (at most `maxCameraFocusU`), kept low so a knocked-down monkey stays in view

### Fight Loop

- Rest: a pause of `restS`, `restFinalS` in the last third. A thrown banana on the crown takes `bossBananaDamage` off `bossMaxHealth` at any time, resting or attacking
- Attack Round: two bites, plus the tail sweep between them once `sweepFromLostShare` (a fifth) of the health is lost, plus minions first once two thirds are; warnings shrink to `finalWarnScale` in the last third. In normal mode the last third is the Minion Finale: each round is a single bite or sweep, taking turns, and minion waves stream alongside on their own, so at most two attacks run at once; a wave and a bite or sweep take turns, each waiting for the other to near its strike, so their dangerous parts overlap by at most `finaleOverlapS`; each re-aimed with a new gap, `minionStreamGapS` after the last one ended, until the king or the monkey falls. Waves lock their own aim and keep their own strips, so they never disturb a bite's or sweep's markers. The first minion attack of a game opens with "Minions, help me!", said once per try (a revive resets it); the king's lines go by priority: "Stay away from me!" first, then the minion call, then the rest, a held line blocking lower ones, and a minion call cut short by the push line is said again right after it
- Mode Choice: the first try is normal mode. After each revive the fight opens with only two banana branches, `choiceSideDeg` left & right of the monkey and `choiceAboveU` above it: a plain banana tagged EASY and a golden one tagged NORMAL (`BossFightHud.SetChoiceTags`). An animated hint icon prompt (hold, then release) sits between them, with a dotted line pulsing toward the crown (on the first try they sit on the first arena banana). The first banana grabbed picks the mode, is thrown at the king like any other, and only then do the arena's banana branches grow
- Easy Mode: every attack is a notch softer: the king has `easyHealthScale` of `bossMaxHealth`; warnings last `easyWarnScale` times as long (in the last third the normal length instead of `finalWarnScale`); rests last `easyRestS` (`easyRestFinalS` in the last third) instead of `restS` / `restFinalS`; the last third keeps its rounds of bites, sweep and minions, no finale; a minion attack has `easyMinionWaves` waves. Normal mode is the fight as tuned
- First Hit: until it, the king only rests; the first hit ("How dare you!") starts the loop

### Attacks

| Attack | Warning | Effect | Dodge |
| --- | --- | --- | --- |
| Bite | a dark red disc on the trunk at the monkey's spot whose bright center fills it, and a red "!" sign reading BITE!, `biteWarnS` | fatal within `biteRadiusU` of the spot | orbit or climb off the disc |
| Tail Sweep | a red band round the trunk at the monkey's height, growing to the swept height, and a "!" sign reading TAIL!, `sweepWarnS` | the tail loops the trunk in `sweepTimeS`, fatal within `sweepBandHalfU` of the ring | climb above or below the ring |
| Minions | `minionWaves` waves, each re-aimed at the monkey: a vertical danger strip down the trunk for each of `minionCount` - 1 fall lines spread `minionSpreadDeg` apart, covering the minion's whole fall path, from the king's coil down to where minions are gone (`guardDepthU` below the entry), all red from the start and beating faster and brighter as the drop nears, one inner line left empty as the gap, and one "!" sign reading LOOK UP! on a middle strip just above the monkey, `minionWarnS`; the first minion attack of a game holds "Minions, help me!" in the bubble | green minions appear under the king's coil and fall one after another (`minionStaggerS`) down their lines; their touch is fatal, unless the gold shield takes it | orbit into the gap |
| Head Butt | the head rears back, `pushWindUpS` | a knock-down; a monkey at the ceiling or touching the resting king (`BossKingContact`; a striking head's touch is ignored, as it reaches far below the rest pose) gets it any time, at most once per `pushCooldownS`, with "Stay away from me!"; while the head is busy with a bite the shove lands at once | stay below the ceiling |

Every warning sign is a red diamond with a white "!" and a word, built by `BossFightHud.AddWarning`, beating faster and growing as the hit nears. A knock-down is the usual stun and drop with the heavier shake (`HitBranchSensor.ReportHit`).

### Fruit

- Banana Branches: `bananaCount` real `BranchWithFruit` prefabs, always bearing, on the segments' branch ring (`branchRadiusU`) round the trunk, plus `bananasPerThird` more at random spots each time the king loses a third of its health; tagged `Branch` as ever, so they knock the monkey down like any branch. Every arena fruit, the tree's own plain bananas included, shows the watermelon mesh (`watermelonMesh`, `watermelonScale`, `watermelonOffset`) in place of the banana model, so the grab and the throw are unchanged and the thrown copy is a watermelon. A taken banana grows back after `bananaRespawnS`, shortened by `bananaRespawnScaleByThird` later in the fight
- Throw: in the arena every grabbed banana, the arena's or the tree's own, still restores stamina and is also thrown: `ArmController` hands it to `BossFightController.TryThrow`, which flies a larger copy (`thrownBananaScale`) in an arc (`throwArcU`, `throwTimeS`) onto the crown, spinning and dropping a trail of green dots, and bursts it into green cubes on impact
- Golden Banana: the same banana branch, its banana tinted gold with a thick gold outline (a copy of `goldenOutlineMaterial`, `goldenOutlineWidthScale` times its width, so it follows the banana model's scale); it first shows up `goldenFirstDelayS` after the first hit on the king, then `goldenIntervalS` after each visit, beside the monkey (`goldenSideDeg` round the trunk, `goldenAboveMonkeyU` above it, under the ceiling) with a "Golden watermelon!" word, and vanishes after `goldenShowS`; grabbed, it refills stamina and raises a shield, gold armor on the monkey (a back plate, two shoulder pads and a helmet), that turns the next fatal hit into a knock-down ("Saved!"); none shows up while the shield is on
- Golden Hint: each time the golden watermelon shows up, besides its word over it, a banner low on screen says "Golden watermelon!" / "Grab it for a Golden Armor" (`goldenHintTitle`, `goldenHintLine`, `goldenHintS`). A taken arena watermelon grows back after `bananaRespawnS` (9 s), scaled by `bananaRespawnScaleByThird` in the later thirds, unchanged; the arena branches' own fruit never grows back
- After The Win: the king's defeat takes every arena watermelon away, the arena's and its branches' own (`RemoveArenaFruit`), their branches left bare, so none can be grabbed and eaten like a banana on the climb after
- Starting Armor: the fight begins with the shield already up, under a short banner low on screen, clear of the key prompt: "You get a Golden Armor!" / "Blocks 1 fatal hit", plus "Grab one: EASY or NORMAL" after a revive (`armorIntroTitle`, `armorIntroLine`, `chooseLine`); it stays until the first banana is thrown; once the armor is spent, the golden banana brings it back. The armor also takes the guarding great cobra's bite (see Win Reward)
- Revive: every time (`canRevive`), a loss in the boss fight stops after the slow-motion K.O. on a "Revive?" card: time stands still and Space or Watch Ad plays a full-screen mock ad that stays until skipped, with no countdown; its Skip button (Space) shows up after `adSkipAfterS`, that plugs USC CSCI 526 (Advanced Mobile Devices and Game Consoles), the course this game is made in, with a Course Page button (Enter) opening `courseUrl`; R, Give Up or `reviveOfferS` running out ends the run as usual. The Skip wait holds while the game is paused. From the K.O. until a revive lands, the monkey is untouchable like in a pineapple leap (`GameController.SetLeaping`), so no web, cobra or branch can catch it on the way off the tree or back; a web holding it is let go at once (`SpiderWebStruggle.ForceRelease`) and the guard ends any peek. After the ad the fight starts over: the monkey flies back (`reviveReturnSpeedU`) to where the fight began and lands on the ring there (`MonkeyController.SyncOrbitAngle`), a spot kept clear of webs, in the armor with full stamina, against a full-health king, and picks a mode again; no grace time and no extra rest

### Spider Webs

`webCount` webs (the segments' `SpiderWeb` prefab) hang at random spots on the trunk when the fight begins, from `webBelowEntryU` below the entry up to just under the ceiling, `websPerThird` more with each lost third, kept `webFruitGapDeg` round the trunk from any banana and never on the monkey. They trap and release as usual, but in the arena each escape press counts `webEscapeGainScale` times (`SpiderWebStruggle.EscapeGainScale`), so mashing Space shakes a web off within the king's attack gaps; outside the arena the scale is back to 1. No two webs overlap: a web keeps `webSeparationU` from any other and `webBranchGapU` from an arena branch's root. Below the arena floor, from the guard up to 3 u under the entry, the climb's own webs grow too, `lowWebAttemptsPer10U` rolls per 10 u at `lowWebChance`, as it had there b4 the bare boss zone.

### Rough Bark

The teammates' rough bark (`RoughBarkZone`, `roughBarkMaterial`) grows on the arena's trunk when the fight begins as the climb's `RoughBarkSpawner` grew it there b4 the bare boss zone: from the guard up to the nest, per 10 u `roughBarkAttemptsPer10U` rolls at `roughBarkChance` (the climb's curve at that height), the climb's sizes (`roughBarkHeightRangeU` 5 to 8.5 u, `roughBarkArcRangeDeg` 110 to 170 deg), each patch at its own height band, its arc clear of the fruit branches and webs (the climb's capsule check, `IsArcBlocked`; an obstacle branch may stick out over a patch, else the arena's many branches leave no room), and never over the monkey's start spot; if the rolls leave none where the monkey fights, one is placed there. Each is built on the tree segment at its height, so it hugs the faceted trunk, then moved under the fight so no segment recycle carries it off; patches stay through a restart.

### Finisher

The last blow either way plays in slow motion: time runs at `finisherTimeScale` for `finisherRealS` of real time. A win flashes the screen white once, then the king shatters (the win reward below); a loss flashes the screen red, shows a K.O. banner and spins the monkey off the tree (`MonkeyController.IsHeld` & `PullTo`), then the revive offer shows. A catch by the guarding great cobra below the arena counts as a boss fight loss too: `CobraSensor` hands it to `BossFightController.TryAbsorbCobraCatch`, which plays the same slow-motion K.O. and offers the revive.

### Victory

At zero health, after the finisher, the king shatters into tumbling cubes; its crown flies in an arc onto the monkey's head in `crownFlyS`, shrinking to `crownScaleOnMonkey`, and stays there.

### Win Reward

The run does not end: `victoryDelayS` after the crown lands the boss bar goes, the armor pieces swirl in from around the monkey and snap on with a gold burst, flash and pulse (`armorRewardS`, `armorRewardRadiusU`), and a GOLDEN ARMOR badge pops in at the bottom right, staying until the armor is spent (`BossFightHud.ShowArmorBadge`); then time stops on a reward panel, "You win!" / "Golden Armor is yours to keep!" / "It blocks 1 fatal hit" (`winTitle`, `winLine`, `winDetailLine`), till Space or its Continue button, and only then does the climb go on. The hazard spawners the fight stopped start again and the great cobra stops guarding (`CobraMotor.StopGuard`), chasing again from `winCobraDropU` (45 u) further down as a head start, about 60 u below the monkey, and a second after Continue a short "The cobra is coming!" / "Climb!" banner calls the chase; the ceiling and camera return to normal. The armor keeps following the monkey and takes one great cobra catch: `CobraSensor` asks `BossFightController.TryAbsorbCobraCatch`, which spends the armor ("Saved!"), throws the head `armorKnockBackU` (20 u) back down the trunk (`CobraMotor.KnockBack`) and freezes it for `armorCobraFreezeS` (15 s, longer than a coconut's 10 s, with the same freeze tint, popup and timer); further contacts within a second are ignored. In the fight the armor's save on the guard neither throws nor freezes it: it only ends the guard's peek and shows the same "Saved!" as any fatal hit, and flies the monkey back up to where the fight began, untouchable on the way and safe from the king's hits till it lands (`ReturnToStart`), so an armored monkey never ends up below the guard, where it could not climb back alive. `GameController.WinGame` and `GameState.Won` have no caller now

UI (`BossFightHud`) is built at runtime on the root Canvas, under the screens: entry flash, name and health bar, speech bubble over the king's head while it is well in view, else docked under the health bar so no line is missed, key prompt, EASY / NORMAL tags, damage numbers and words, banners (armor intro, K.O., win), the revive card and the mock course ad.

## Spider King (Boss 2)

The `Boss/SpiderFight` prefab (`SpiderFightController`, `SpiderKingBody`, `BossFightHud`) sits at the root of `FullPlayScene`; like the Cobra King, everything is built at runtime from primitives.

- Trigger: once the Cobra King is beaten (`BossFightController.IsDone`) and the monkey has climbed `spiderTriggerHeightU` (1050 u, `WorldBalanceConfig` Spider Fight group) in Playing and not mid leap. Never in Endless mode
- Bare Zone: `spiderZoneStartU`~`spiderZoneEndU` (1025~1100) counts as a boss zone in `RampedDifficultyManager.IsInBossZoneY`, so no branch, web, falling cobra or rough bark is generated there; branches left near the arena are removed when the fight begins
- Arena: `arenaHeightU` above the entry, the climb capped there (`SetHeightCeiling`); the great cobra guards `guardDepthU` below (`StartGuard`), the camera pulls back `cameraDistanceScale`
- Spider: crawls on the bark (`SpiderKingBody`: dark body, red back mark, small gold crown, 8 legs, 6 eyes), stalking the monkey till `stalkDistanceU` away. A red dotted ring of `territoryRadiusU` round it on the bark is its territory, brighter while the monkey is inside
- Health: `spiderMaxHealth` (10)

### Silk Knots (how to win)

- `knotCount` glowing silk balls hang on banana branches round the arena (the banana model hidden, its collider kept), grabbed like any fruit; `ArmController` hands the grab to `SpiderFightController.TryGrab`
- A rip takes `knotDamage` (1); a knot inside the territory glows gold and takes `territoryKnotDamage` (2), and makes the spider attack sooner
- Repair: a ripped knot within `repairRangeU` of the spider is crawled to and re-spun in `repairS`, healing `repairHeal` (1), at most once per `repairCooldownS`; a far knot just grows back after `knotRegrowS` with no heal

### Attacks

Every attack is telegraphed by the eyes glowing red over its wind up, plus a red "!" sign (`BossFightHud.AddWarning`).

| Attack | When | Effect | Counter |
| --- | --- | --- | --- |
| Web Shot (WEB!) | out of the territory | a silk ball flies to where the monkey was `webShotLockS` before the shot and sticks a real `SpiderWebTrap` there; a miss stays on the trunk as a sticky web (`maxStickyWebs`, the oldest goes) | keep moving |
| Silk Hook (SILK!) | `dragChance` within `dragRangeU` | a silk line reels the monkey along the bark at `dragSpeedU`; reaching `dragReachU` is a wrap (fatal) | tap Space `dragEscapeTaps` times, or grab anything |
| Cocoon | the monkey is trapped in any web | the spider crawls over at `cocoonSpeedU`; arriving within `cocoonReachU` is a wrap (fatal) | mash Space to break the web first |
| Leg Swipe (SWIPE!) | the monkey in the territory | front legs rear up, a knock-down within `swipeRangeU`, never fatal | leave the ring |

Rest between attacks is `restS`, times `territoryRestScale` while the monkey is in the territory, down to `lowHealthRestScale` as health runs out.

### Fruit Lure

- `lureFruitCount` bananas (normal, regrow after `lureRespawnS`); a grab restores stamina and tosses a banana copy round the trunk to the side opposite the monkey, away from the spider, since the player cannot aim
- The spider drops its wind up, runs there (`lureRunSpeedU`) and eats `lureEatS`, taking its territory along: the window to rip the knots near it safely

### Armor, Loss & Win

- Armor: the Cobra King's gold armor, if not used on the climb (`BossFightController.HasArmor`), saves one fatal hit (`TrySpendArmor`): the spider is stunned `armorStunS` and backs off; a used armor stays used
- Great Cobra: a catch by the guard is a fatal hit too (`CobraSensor` asks `SpiderFightController.TryAbsorbCobraCatch` first)
- Loss: the same slow K.O., revive offer and mock course ad as the Cobra King; a revive restarts the fight at full health
- Win: the spider falls off the trunk, a reward panel stops time till Space or Continue: the Silk Gloves, `SpiderWebStruggle.EscapeGainScale` = `glovesEscapeGainScale` (2) for the rest of the run; then the great cobra chases again from `winCobraDropU` down
- Test: the `SpiderJump` hack key (F9, `HackShortcutConfig`) skips the Cobra King as beaten (`SkipForTest`, armor worn) and teleports the monkey to `spiderJumpHeightU` (1000 u)

## Known Gaps

- Mob tuning for the web escape and web geometry lives on scene components, not in a shared ScriptableObject; only the web spawn rates and attempt counts are in `WorldBalanceConfig`, and the falling cobra and great cobra numbers in `CobraBalanceConfig`
- Stamina is not linked to branch hits
- `FullPlayScene`'s Canvas holds 2 objects named `ProgressBar`, the climb progress and the web escape bar, so look them up by path
