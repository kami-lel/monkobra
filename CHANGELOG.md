# Monkobra CHANGELOG

<!--
Fixme 1st pineapple/banana should appears on top of player
Bug endless mode need to start w/ full heath & no starting zone
Todo upd gdd
-->


## [Unreleased]

### Added

### Changed

##### Wenhai Dong

- fewer falling cobras after the boss fight: `fallingCobraSpawnProbabilityCurve` drops to 0.1 right past difficulty 1 & rises to 0.2 by 2 (was 0.2 to 0.35); the climb b4 the boss is unchanged

### Deprecated

### Removed

### Fixed

### Security

[unreleased]: https://github.com/kami-lel/monkobra/compare/v1.0.0-beta.2...dev













## [1.0.0-beta.2] - 2026-11-10

### Added

##### Erik

- green cobra proximity vignette: grows from bottom half to full ring as the Great Cobra closes in, beating faster per stage; hidden in the starting zone & while the cobra is frozen, guarding or the monkey leaps
- stamina & cobra vignettes alternate, never overlap

### Changed

##### Erik

- mock analytics session simulates real play: climb, hazards, fruit, cobra chase, boss fight & score follow the real rules, so death heights, durations & scores read like a playtest
- Great Cobra speed scales w/ gap to monkey: far-behind cobra runs faster, close one slower; `cobraBaseSpeedCurve` now a 0~2 multiplier of monkey top climb speed, new `cobraDistanceSpeedCurve`

##### Wenhai Dong

- boss fight difficulty restored to pre-bare-zone tuning: arena grows own branches, rough bark & webs like the climb; `bossMaxHealth` back to 12
- boss fight: tail sweep joins at 1/5 king health lost, not 1/3; arena watermelons regrow slower (6 → 9 s) & are removed after the win; golden watermelon banner; webs never overlap
- boss fight: minion call never lost, repeated after a revive

### Removed

##### Erik

- Great Cobra close-range slow-down ⇐ distance curve replaces it

[1.0.0-beta.2]: https://github.com/kami-lel/monkobra/compare/v1.0.0-beta...v1.0.0-beta.2













## [1.0.0-beta] - 2026-11-10

### Added

##### Belle Dai

- rough bark: broad, raised, wavy-edged patches that hug the faceted trunk & smoothly slow climbing & circling alike while touched; overlaps never stack the slowdown (`RoughBarkZone`)
- rough bark placement: patches rolled per tree segment off the ramped difficulty, arcs kept apart so they never close into a full ring, so a path round always stays open (`RoughBarkSpawner`)
- rough bark art: painted albedo & normal map textures on the `RoughBark` material
- slippery bark prototype, reworked into rough bark before release: wet patches w/ a guaranteed tutorial band, a short eased slide & loosened grip that slowed the arm stroke, own test scene, an Editor installer & an automated play-mode smoke check driving the real monkey

##### Erik

- scene system: title, level select (Normal Play, Endless Mode, Back), `Lv1Scene` & `EndlessScene`; `SceneNames.Load` loads every scene
- Endless Mode: starts at the tree base in the post boss state, difficulty keeps ramping; unlocks once the Cobra King is beaten (Editor menu `Monkobra/Unlock Endless`, `Monkobra/Reset Progress`)
- pause & lose screens: Resume, Restart, Exit to Title by keys, mouse hover or click (`RunScreenMenu`)
- analytics: 1 session file per launch across scene loads, runs tagged Full or Endless; every run event is an `AnalyticsMetric`, so a new metric is 1 entry; records leaps, rough bark, freezes, pauses, boss fight events & the Starting Zone Exit
- dev builds only: Download Analytics button on the lose screen; hack keys F1~F12 for no-clip, max stamina, speed x3, boss jump, spawning a pineapple branch, coconut branch, falling cobra or spider web, report download, new session & mock session
- `Lv1Scene` split into nested prefabs, so teammates edit different files
- Great Cobra exit boost after the monkey leaves the starting zone, w/ "Run! The Great Cobra is chasing you!" warning
- climb speed builds up along curves of key hold time; branches cleared round the monkey before the run is playing
- fruit fly: a grabbed fruit's sprite flies to the stamina bar, coconut HUD or monkey
- pineapple leap: a grab pulls the monkey up, hazards off for the leap & a short landing grace
- coconut throw: a double-tap spends 1, it falls & freezes the cobra on touch or after a fixed distance; brown coconut model & new fruit sprites, credited in `CREDITS.md`
- consistent pop up UI: every center message is a variant of `PopUp/_PopUpBase`, shown 1 at a time by `PopUpManager` w/ priority & queue, see `docs/pop-up-doc.md`
- tree bark texture on trunk segments, credited in `CREDITS.md`
- camera redesign: every gameplay camera tracks `CameraAnchor`, so strafing no longer spins the view; climb framing, hold curves & noise on rough bark; all tuning in `CameraConfig`
- shot director (`CameraRigManager`): coconut, cobra alert & pineapple leap shots by priority, so events never chain into rapid cuts
- height marker pass trigger (`HeightMarkerTrigger`)
- difficulty: the post boss ramp keeps rising after the fight; a boss zone holds only the boss fight; 4 branch kinds (empty, banana, coconut, pineapple) w/ own chance curves; a guaranteed pineapple & coconut branch at fixed heights

##### Wenhai Dong

- Cobra King boss fight (`Boss/BossFight` prefab): at the trigger height the run enters an arena; the king coils round the tree top, alternates rests with attack rounds (bite, tail sweep, minion waves, head butt) that grow with each lost third of its health
- boss arena: banana branches whose fruit is also thrown at the crown, spider webs, a golden banana that refills stamina & shields 1 fatal hit; the monkey's climb is capped below the king, the great cobra guards below
- boss fight presentation: danger zone warnings, health bar & speech, first-throw key prompt, slow-motion K.O., camera pulls back & tilts up
- boss fight win: the king shatters, a reward panel shows, the monkey keeps a Golden Armor that absorbs 1 great cobra catch, the run goes on
- boss fight revive: every loss offers a revive for watching a mock ad; after a revive the first throw picks EASY or NORMAL
- boss fight height marker: a bigger, red, beating plate w/ a Cobra King look-alike, hidden once the fight begins
- `GameState.Won`: a won run settles the score & analytics records it as `RunStatus.Won`
- pause: Esc or P pauses or resumes a live run, R restarts it; time freezes, audio pauses, a `PauseScreen` panel shows
- tree bark less glossy but still crisp at steep angles

##### Yuqing Lu

- coconut: a rare fruit grabbed like a banana, scores w/ a multiplier & restores no stamina
- coconut use: a quick double-tap freezes the great cobra for a while; refused while paused, trapped in a web or already frozen; the cobra turns light blue while frozen
- coconut HUD: count, freeze countdown & bar, a double-tap hint until the first use, center messages; coconut analytics per run
- green coconut model & matching UI sprite from the PolyOne Free Fruits pack, credited in `CREDITS.md`
- trunk height markers: every 50 u a number beside the trunk in m on a dark rounded plate, 1 on each trunk edge as seen from the camera; the marker grows & brightens the first time the monkey passes it

##### Houdong Pan

- lose screen leaderboard: 9 rivals named from a joke pool (BananaKing, PeelMeDaddy, CSCI526, ~) w/ scores rolled in a range, ranked w/ this run's final score, ties going to the player, & a "YOUR RANK #n / 10" line (`LeaderboardWidget`, `Leaderboard` & `LeaderboardRow` prefabs)
- rivals stay fixed for the whole playtest, so every restart faces the same board; rolled on their own random generator, so hazard spawns are untouched (`SessionLeaderboard`)
- fruit grab prompt: a keycap "space, hold, then release" follows the fruit in reach & teaches the first grab of each fruit kind per run, hiding while a screen is open (`FruitGrabPrompt`)
- reusable keycap widget: the boss fight's keycap pulled out into 1 runtime widget, built in code w/o sprites or prefabs, now shared by the fruit tutorial & boss HUD (`KeyPromptWidget`)
- `ArmController.FruitGrabbed` event after every successful grab, for tutorials & HUD to listen to

### Changed

##### Belle Dai

- falling cobra strikes once: after dropping the monkey it switches itself off, so it can neither keep falling nor hit again; the monkey sensor & the snake share 1 hit guard, so a contact never counts twice (`FallingCobraController`)

##### Erik

- starting zone: opens at lower stamina, exhaustion slows less, idle recovery slower, exit trigger raised; Starter Zone → Starting Zone
- pause & restart are `InputSystem_Actions` actions read by `GameController`
- fruit grab aims at the closest banana; stamina restore & bar punch land when the fruit's flight arrives
- scripts renamed to house role suffixes & code style pass over all scripts; no behavior change
- prefab restructure: `World` & `Systems` wrappers dropped, `Analytics`, `InGameHUD` & `PopUps` split out; `CoconutManager` centralizes the coconut count, throw & HUD
- banana model: `SM_Fruits_Banana_Y1` replaces the stretched capsules
- config reorganization: `GameBalanceConfig` split into `PlayerBalanceConfig`, `WorldBalanceConfig` & `CobraBalanceConfig`, `MonkeyConfig` into arm & input configs, `MovementConfig` → `MonkeyClimbConfig`, `Settings/` 1 folder per module; every value carried over
- camera: `CameraEfxConfig` → `CameraConfig`, `CameraLookUp` → `CameraClimbFraming`; low stamina pulse vignette only; hit shake scaled per kind; `CameraRigManager` no longer toggles the shared Interact action
- `DynamicTreeSegmentPool` holds more segments & looks further ahead
- height markers show up to 50 u ahead & pulse on touching their trigger
- ramped difficulty runs 0 at the starting zone exit to 1 at the boss fight; first appearances follow height
- first-grab fruit popup also detects an unlearned fruit nearby; falling cobra stops against tree branches; boss arena fruit shows the watermelon model

##### Yuqing Lu

- `Coconut` prefab: from banana-shaped placeholder to the coconut item w/ its own score kind & outlines
- `ScoreManager` scores & counts each pickup by `PickupKind`; a coconut's flight restores no stamina; analytics splits reward score into banana & coconut score
- progress bar cobra gap in m, matching the height markers; `unitsPerMeter` now only sets the distance score rate

##### Houdong Pan

- progress bar redrawn: monkey & cobra faces drawn from 1 generated antialiased circle, no imported art; an upright distance readout & bracket span both markers; the cobra face slides aside when the 2 get close (`ProgressBarWidget`, `ProgressBar` prefab)
- tutorial & HUD wording: "Grab Banana" → "Grab Fruit", low stamina hint reads "Low Energy"

### Removed

##### Erik

- `devYuqingCoconutScene` & `SlipTestScene` personal dev scenes
- `upSpeedU`, `downSpeedU` & `accelerationU`, replaced by the climb speed curves
- orphan `GoalText` object in `Lv1Scene`
- look-left & look-right cameras: a grab shifts the climb view instead of cutting
- cobra alert on surges, falling cobra hits & proximity
- `CoconutSpawner` & the `CoconutSystem` prefab, replaced by the coconut branch kind; fixed coconut start height, rough bark score gate & forced first patch: the ramp decides

##### Yuqing Lu

- HUD Height label: the trunk height markers show the climbed height

### Fixed

##### Erik

- restart could spawn the monkey inside a branch
- analytics dropped starting zone data; a run now opens when the zone begins
- camera dizziness: world spin on strafe, background judder, side flip while grab held, nod on every up press
- hit shake reached only the behind view; stamina pulse could leave a widened FOV on the old camera

##### Yuqing Lu

- coconuts never spawned in `Lv1Scene` ⇐ `CoconutSystem` prefab missing under `Envs`

##### Wenhai Dong

- boss fight: an armored monkey could drop past the guarding great cobra & never climb back alive; the armor's save now flies it back to the fight's start, untouchable on the way
- boss fight: pineapples still grew in the arena ⇐ they score as bananas; now told apart by `LeapOnGrab`
- a branch hit in the starting zone showed "The cobra is speeding up!" ⇐ mistakes counted as surges there

[1.0.0-beta]: https://github.com/kami-lel/monkobra/compare/v1.0.0-alpha.2...v1.0.0-beta













## [1.0.0-alpha.2] - 2026-10-19

### Added

##### Erik

- `HackShortcutConfig` asset: all dev keys in one place, dev builds only
- F8 downloads the analytics session report in the browser (WebGL dev build)
- F1 toggles the cobra no-clip cheat
- `HackShortcutHelp`: TMP text listing the dev keys, placed on the `Lv1Scene` canvas

### Changed

##### Erik

- analytics F9 & F10 keys moved to `HackShortcutConfig`, dev builds only
- WebGL build profiles (`GithubPages`, `ItchIO`) now build as development builds, so the dev keys work in the browser

[1.0.0-alpha.2]: https://github.com/kami-lel/monkobra/compare/v1.0.0-alpha...v1.0.0-alpha.2

## [1.0.0-alpha] - 2026-10-19

### Added

##### Yuqing Lu

- tutorial goal line above the keys
- `Energy` label on the stamina bar
- first low-stamina hint: tells the player to eat bananas, once per scene load

##### Belle Dai

- keyboard-style tutorial controls: a WASD cluster & wide Space key built from editable uGUI rectangles, no sprites, w/ inset keycap frames, bevel lines & a caption per key
- tutorial UI font: a static subset of Noto Sans TC holding ASCII & the Chinese glyphs used, so WebGL needs no system font; shipped w/ its SIL Open Font License

##### Erik

- camera cool-down effect: vignette & zoom-out pulse on move attempts at low stamina, tuned in `CameraEfxConfig`
- camera hit shake: light for branch hits, heavier for falling cobra
- starting zone: live run before Playing begins, ended by the monkey entering an exit trigger; Great Cobra holds height meanwhile
- ramped difficulty starts at 0 at the starting zone exit
- grab arm tracks its banana while interact is held

### Changed

##### Yuqing Lu

- tutorial keys & start prompt moved down to make room for the goal line

##### Erik

- difficulty ramp & branch/banana curves start above the starting zone exit
- Great Cobra starts lower (-5 → -10)
- low stamina, not only empty, now triggers move-attempt feedback
- `Lv1Scene` split into prefabs, easing scene merges
- Mobile render profile now renders at full scale (1.0, was 0.8), matching the PC profile

##### Wenhai Dong

- web builds lock to 720p w/ scaling UI layout, fit any screen & density
- web builds use new `FitWindow` template: 16:9, scales to browser window, sharp on Retina & 4K

##### Houdong Pan

- more falling cobras: 3 spawn attempts per segment (was 2), & the chance curve climbs to full instead of topping out at 0.4

### Removed

##### Erik

- timer-based stamina drain & its `GameBalanceConfig` fields

### Fixed

##### Erik

- stamina bar dropping by a lump while moving, stunned, or after releasing a key

[1.0.0-alpha]: https://github.com/kami-lel/monkobra/compare/v0.9.0...v1.0.0-alpha













## [0.9.0] - 2026-10-12

### Added

##### Erik

- endless tree: `DTSPool` recycles the lowest segment over the top as the monkey climbs
- spider web & falling cobra spawn in `GameBalanceConfig`: an attempt count per segment and a probability curve of the ramped difficulty, the same model as branches
- analytics: a markdown session report per launch, saved during play, w/ Mermaid graphs per run (score split, height & stamina vs time) & a summary of lost runs (death causes, final score, height, duration)
- analytics records each run's death cause, inferred from the events just before the loss, & the death height
- analytics marks a run cut short by stopping Play mode, a scene reload, or F9 as `Interrupted`
- `AnalyticsConfig` asset: analytics keys, sample & autosave intervals, death cause window
- F10 saves a fully mocked session report, for checking it without playing

##### Yuqing Lu

- spider web trap: webs spawn on the upper trunk clear of branches & fruit, hold the monkey on contact, & break on rapid Space presses; first-trap hint, escape bar, short immunity after escaping
- `SpiderWebSpawner` test override: webs at a flat chance at any height
- `RampedDifficultyService.GetClimbedYDistance`: height of any y above the player's start

##### Belle Dai

- falling cobra: a new hazard that perches on a real branch just above the monkey's path round the trunk, flashes a red warning, then drops on it; built w/ its own `FallingCobraDemo` test scene, then placed in `Lv1Scene`
- falling cobra spawns grow more frequent the higher the monkey climbs
- falling cobra hit: 1 trigger covers the whole snake, so a hit makes 1 drop & 1 camera shake, not 1 per body segment

##### Houdong Pan

- scoring system: points per meter of new best height only, so falling & re-climbing earns nothing, plus a reward per banana, shown live on a HUD score label; the score freezes when the run ends (`ScoreManager`, `ScoreDisplay`)
- each fruit pays its reward exactly once, even when both hands claim it, & never after the run ends (`ScorePickup`, `ScoreReward`)
- `ScoreConfig` asset: points per meter & units per meter; rewards open to future sources through `ScoreManager.TryAddReward`

##### Wenhai Dong

- great cobra base speed rises w/ the ramped difficulty
- great cobra mistake surge: each branch or falling cobra hit & every web trap speeds it up for a while, stacking; on-screen cue & red cobra marker while it lasts
- falling cobra wakes from farther away while the monkey reaches for a banana, & some ambush just above a banana
- falling cobra re-hit tuning: grace time & max hits per snake
- game balance: Great Cobra group in `GameBalanceConfig` & a playtest-driven pass over every hazard curve, for a 7~8 min run w/ the cobra pressing harder as the climb goes on

### Changed

##### Erik

- falling cobras roll per tree segment like branches, wake near the monkey, & fall at a constant speed while sliding toward it; the global timer, unlock height & one-at-a-time limit are gone
- spider webs spawn from an attempt count & a difficulty curve, replacing the start height, full-chance height, max chance & slot count
- analytics tracks every web encounter & records the highest height as a float
- `docs/work-with-git.md` asks for a short PR description, since it feeds each member's Weekly Progress

##### Yuqing Lu

- webs spawn per tree segment & measure height from the player's start, replacing a one-off pass at `Start`

##### Wenhai Dong

- great cobra: bead-chain body w/ taper, hooded head, slither wave, & a corkscrew coil of at least 1 loop round the trunk, so dropping onto it or orbiting into it is fatal
- balance retuned for a 7~8 min run: ramp reaches max at 1200 u (was 500), more branches, steadier bananas, idle stamina recovery 2 → 2.5 per s, banana restore 30 → 35
- `Lv1Scene` progress bar shows 60 u below the monkey (was 15)
- web & trap box 20% larger

##### Belle Dai

- falling cobra in `FallingCobraDemo` uses the branch-hit stun & drop instead of ending the run

### Fixed

##### Yuqing Lu

- webs missing from `Lv1Scene` since the endless tree

##### Wenhai Dong

- cobra contact script failed to load ⇐ class name mismatch
- cobra contact now counts the monkey's tail & hands, & overlap, not only body entry
- cobra no longer retreats w/ a descending monkey or parks at its height, so body contact kills at once
- monkey body collider matches its visible body, so contact registers from its bottom
- falling cobra drops the monkey on any-part contact, & no longer pushes it down several times in a row
- webs make the great cobra surge on every trap, not only the first

##### Belle Dai

- falling cobra warning starts red at once & pulses from that moment
- falling cobra spawner works on the endless tree

[0.9.0]: https://github.com/kami-lel/monkobra/compare/v0.1.1...v0.9.0

## [0.1.1] - 2026-09-29

### Added

- fruit grab: hold interact to stretch arm toward newest fruit, release to grab iff hand overlaps it
- fruit grab restores stamina
- movement-based stamina drain: climbing costs most, orbiting less, descent free
- `GameConfig` asset centralizing game-wide tuning

### Changed

- stamina tuning → `GameConfig`
- `StaminaBar` reachable as singleton `StaminaBar.I`
- tuning assets → `Settings/`
- arm reach: aim locks at press, replaces spring/overshoot reach
- arm joint reach limit ↑
- `Lv1Scene` camera views retuned

### Removed

- legacy arm drivers `MonkeyArm` & `HandHandler`, superseded by `ArmRoot`
- unused reach tuning from `MonkeyConfig`

[0.1.1]: https://github.com/kami-lel/monkobra/compare/v0.1.0...v0.1.1

## [0.1.0] - 2026-09-28

### Added

- Unity 6 URP project scaffold, `Lv1Scene`
- `README.md` game design & controls, `AGENTS.md`, `CONTEXT.md`
- monkey movement: orbit trunk, climb w/ accel/decel ramp, Input System controls
- hand-over-hand arm climb stroke on physics-jointed arms, tail
- Cinemachine camera rig: behind view, left/right side views driven by fruit-reach detection
- climbable tree from static & dynamic trunk segments under `TreeTop`
- procedural branch & banana placement, tuned by `BranchFruitPlacementConfig`
- branch-hit penalty: camera shake, stun, drop
- stamina bar UI w/ timed drain
- tree-progress bar tracking player height
- cobra chasing up trunk, game-over on contact
- win zone trigger, `WinScreen`, `ScreensManager`
- `MonkeyConfig` asset centralizing monkey tuning
- scene singletons `GameController`, `TreeManager`
- WebGL build served from `Monkobra/` via GitHub Pages

[0.1.0]: https://github.com/kami-lel/monkobra/releases/tag/v0.1.0
