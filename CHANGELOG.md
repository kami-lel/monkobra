# Monkobra CHANGELOG

<!--
BUG cobra king boss fight 1st watermelon tutorial, be popup like
bug make leaderboard works
fixme mpv ramping during normal gameplay
todo lighting improvement
todo vertical version adaptation
todo chinese version
todo add 3x fruit
-->














## [Unreleased]

### Added

##### Yuqing Lu

- beating a boss scores: 1000 points for the Cobra King and 2000 for the
  Spider King, added as the climb goes on after the reward, every win of the
  Hardcore boss rush included

##### Houdong Pan

- Spider King sound cues as each attack charges: spit for a web bullet, fang
  bite for a slash, scrambling legs for a dash; thwack and silk snap at a parry
- cobra alert cut away: black cinema bars grow in top and bottom, the UI hides,
  the monkey's input locks; all return after the shot

##### Erik

- every boss attack shows one standard sign: a red diamond with "!" and a
  one word name in the Impact font (`Bite!`, `Tail!`, `Web!`, `Dash!`,
  `Slash!`), 3 times per attack per session, then none; the Spider
  King's web shot and slash gain a sign; `Monkobra > Build > Warning Font`
  makes the font asset
- the right-hand distance indicator hides during a boss fight
- uniform outline design: banana, coconut, pineapple, the Spider King arena
  melons and the monkey share world-unit widths and are outlined once round
  the whole shape, never per mesh part; a fruit wears 3 tiers in its own hue:
  muted at rest, whitish and thicker when reachable, saturated and thickest
  when grabbable (banana yellow, pineapple orange-gold, coconut brown, melon
  reddish pink); a fruit hidden by the trunk, a branch or the monkey shows a
  soft out-of-focus halo with its own shape cut out; all values in 1 asset,
  `FruitOutlineStyle`
- Spider King web intro: once per Normal Play run, at 250 u climbed, the
  camera eases to the bare trunk, the king scuttles in round it from the far
  side, glows, and spits a web that traps the monkey, so the first web is
  always met with its escape hint; the great cobra holds still until the
  monkey breaks free; never in Hardcore mode
- slower great cobra start: the camera flies its own smooth journey, an eased
  pull away down the trunk round the tree to the cobra, a long look and the
  same way back; the cobra climbs 7 u/s during the pull away, then chases at
  normal speed; UI and "Run!" return the moment the shot ends
- `TutorialSkipManager` (on the across-scene `GameController`): every tutorial
  and cut scene shows once per session, nothing saved, so a replay skips what
  was already seen; `TutorialSkipConfig` turns one item back to every run; a
  boss lesson counts as seen only once that boss is beaten, and the saved
  armor words key is gone
- dev checkbox on `Prefabs/GameController/GameController.prefab` (off by default): skips
  every item at once, the intro comic too; only in Editor/development build,
  never in a release build
- 2 boss dev scenes, `DEVBossCobraKingScene` & `DEVBossSpiderKingScene`: each
  plays the first meeting with one boss, with no starting zone, the fight
  beginning on the first live frame (the climb baseline moves down to the
  boss location, test-only `startOffsetU`), through its own DEV copy of
  `WorldBalanceConfig`; every tutorial off the boss fight is off, the boss
  fight's own follow the `GameController` skip box; the Loop is the integer
  `loopOverride` on the `BossDevStart` object; built by `Monkobra > Build
  Boss Dev Scenes` or headless by `BossDevSceneBuilder.BuildInBatch`
- boss intro cut scene: each boss fight opens on cinema bars, a cinematic
  camera on the monkey and the boss, the monkey's input locked and a boss
  animation, then the bars draw out and the boss name shows; the Cobra King
  feints harmlessly left and right round the monkey, the Spider King drops in
  on a silk line, lands, rears and screeches; the scene skips by
  `TutorialItem.CobraKingIntro` / `SpiderKingIntro`, the name always plays
  (`BossIntroCutScene`, `CameraRigManager.ShotKind.BossIntro`)
- Spider King tutorial popups w/ the animated key icon: tail parry, melon
  throw, dash dodge, web escape, each once a session on its first moment
  (`SpiderKingTips`, `TutorialItem.SpiderTip*`)
- Spider King web shot and wave show a thin danger path from the spider to
  the monkey, and the "Web!" sign rides it
- Hardcore Mode scores 1.5x on every score (`ScoreConfig.hardcoreScoreScale`)

### Changed

##### Yuqing Lu

- Cobra King and Spider King defeat popups tinted in each king's purple

##### Erik

- branch bark outline thicker, 0.03 → 0.05 u
- one `GameController` prefab in every scene now carries the launch singletons
  as components (`AnalyticsManager`, `ControlStyleManager`, `MusicManager`,
  `TutorialSkipManager`); no `Resources.Load`, only it destroys a duplicate &
  keeps itself across loads, so the duplicate warnings are gone
- `Monkobra` menu regrouped: `Build` (UI Fonts, UI Button Prefabs, Title
  Screen, Intro Comic, Outline Materials, Outline Prefab Wiring, Falling Cobra
  Body), `Progress` (Unlock Endless, Reset Progress), then Report Trunk
  Scatter & Reset Leaderboard Emulator Identity
- F3 dev speed buff 3x → 5x
- `GameController` split: `RunController` (renamed, `GameplayScene` only) keeps
  the run state, a new across-scene `GameController` holds the run mode
- falling cobras lighter again: skinned body posed by bones, one shared culling
  group for sleepers, own physics layers, capped trail & smoothing work; frame
  8.4 → 4.2 ms with about 3 live cobras
- branches, spider webs, falling cobras and rough bark now spread by world
  height from a run seed, not rolled per tree segment: even spacing with no
  bare stretches or segment seams, density curves per 10 u of trunk, a
  random tree each run (fixed seed on `TrunkScatter` replays one), new
  `Monkobra > Report Trunk Scatter` evenness check
- boss name font Creepster (OFL) added, built and assigned to both HUD
  configs by `Monkobra > Build > Warning Font`
- boss name shows only above the health bar, in Creepster: it starts big in
  the lower half of the screen, holds a beat, then shrinks as it glides up to
  the bar; the start banners of both kings are gone (`BossFightHudConfig`
  `NameIntro*` values)
- attack sign: only the red diamond and "!" shake (wobble and jitter, wider
  and faster as the hit nears); the word keeps a fixed scale and place
- spider web escape bar fills in toxic purple (`SpiderWebWidget.fillColor`),
  no longer the gold of a reward
- Spider King parry clashes with sparks and slow motion only, no "Nice!" text
- a lost heart or armor shows a `-1` in Anton flying from the monkey to the
  heart row (red) or the armor mark (gold); the heart dims or the mark goes
  as it lands (`BossFightHud.PopLoss`); "Heart lost!" and "Saved!" are gone
- boss damage shows a deep red `-n` in Anton flying from the head or weak
  spot to the health bar; the bar and heart count drop as it lands
  (`BossFightHud.PopDamage`, `BossHitColor`); a kill hit's finisher starts
  at once
- golden watermelon: the floating word and the big toast give way to a
  pineapple-style popup (gold watermelon icon, hold hint, a line to the
  melon) once per session, until one is grabbed (`GoldenMelonHint`,
  `PopUpId.GoldenWatermelonFirst`, `TutorialItem.GoldenWatermelonGrab`)
- golden watermelon grab flies a gold armor sprite from the melon to the armor
  mark beside the hearts (new `FruitFlyTarget.ArmorMark`); the armor counts
  as worn at once, its mark pops in on landing
- no free armor at a first fight's start (Loop 1 and later, both kings): the
  armor popup is gone and the first hit costs a heart; the golden watermelon
  is the only source, and armor won earlier stays; a retry, revive, EASY
  (Loop 0) and the Hardcore start still hand it out
- golden armor is now a golden crown: only the king's crown model sits on the
  monkey's head, every text, badge and HUD symbol says Golden Crown, and every
  golden thing uses `UIStyle.Gold`
- boss fights start when the intro ends, not on the 1st thrown melon: the
  Cobra King's attack loop starts at once, the Spider King's lessons and
  attacks start at intro end
- boss name holds 1.8 s (was 0.9) before it glides up; the -1 and -n numbers
  start larger and punch in at spawn and again on landing
- golden watermelon popup is small plain text, shown only within
  `goldenMelonPopUpRadiusU` (12 u) of the melon
- Spider King web shot and wave are pinkish purple, turning red as they near
- boss hit, parry and dodge points ramp by Loop like the defeat points
- revive goes straight back into the fight, w/o a difficulty card: the Cobra
  King restarts at Loop 0 (EASY), the Spider King on the same Loop

##### Houdong Pan

- cobra alert shot: 2 s hard cut and cut back, no swoop down the trunk; camera
  stands on the head's side of the trunk and the head rears up
- "Run!" and "speeding up" popups smaller, orange and higher; "Run!" waits for
  the UI to return
- spider web brighter with a soft glow; branch bark warmer, less moss, thicker
  outline

##### Wenhai Dong

- the starting zone grows a manageable amount of bananas: each banana branch
  attempt at 0.2 instead of 0.4 (`startingZoneBananaBranchChance`), about 2
  banana branches a segment instead of 4
- no fruit hangs below the monkey's start any more, so none shows up under
  the player at the start: its branch is lifted till the fruit is level w/
  the start, or dropped if it can't be (`KeepFruitAboveStart`)

### Deprecated

### Removed

##### Erik

- one-shot Editor menus already applied: UI Style 3~6 & Build All (level
  select, HUD, pause & lose screens, nickname input), Set Up Falling Cobra
  Layers, Check Outline Style, Prepare Firebase Leaderboard
- `PersistentBootstrapLoader` & the `Resources/` prefabs of the 3 managers
- tracked `tools/music/__pycache__` bytecode
- 5 unused fields of the falling cobra config: body twist, neck thickness &
  neck rounding (3)
- hack keys 1 and 2, the jumps to the boss zone and to the Spider King: the
  boss dev scenes replace them
- the revive's difficulty card, its EASY / NORMAL tags and choice fruits

### Fixed

##### Erik

- Spider King intro: legs keep walking while the king scuttles round the trunk,
  following its speed; new `EnterWalk` config sets the peak
- first falling cobra no longer stalls the game: head template (about 0.6 s)
  builds at scene start, support scan skips duplicate vertices, error check
  runs only in `Falling Cobra Body` build

##### Houdong Pan

- knocked-down monkey no longer cycles limbs while sliding; all 4 limbs grip
  the bark until control returns
- Spider King air wall no longer takes the web's glow, own see-through material

##### Erik

- cobra alert cinema bars now draw out as the camera starts back to the
  monkey, not after it arrives; the UI still returns when the shot ends

### Security

[unreleased]: https://github.com/kami-lel/monkobra/compare/v1.0.0-beta.5...dev













## [1.0.0-beta.5] - 2026-11-24

### Added

##### Yuqing Lu

- intro comic scene between the title and the level selection: five captioned
  motion-comic panels, shown once per launch, skippable with Esc or Skip

##### Belle Dai

- optional Firebase leaderboard on the lose screen: anonymous players,
  persistent per-mode personal bests, top ten, offline pending scores
- editable leaderboard nicknames, saved per device, keeping the same anonymous
  player and personal best

##### Houdong Pan

- cartoon tree: stylized trunk and branches with soft lighting, grain, moss,
  leaves and an outline; colliders, hits and grabs unchanged
- jungle backdrop that changes with height: forest, river, clouds and birds
- synthesized sound effects for the monkey, the Cobra King and the Spider King

### Changed

##### Wenhai Dong

- endless run is a boss rush: the Cobra King and the Spider King take turns every
  300 u, each fight harder than the last of its king
- Spider King easier and fairer: 15 health, slower crawl, bullets and slashes,
  longer rests, starts above the monkey; easy mode much softer
- Spider King slashes thicker and easier to read; web laying no longer stutters
- both kings' health bars show remaining health on a heart; the Spider King's is
  red
- both kings' revive is a plain EASY or NORMAL difficulty card
- Spider King fights gain their own sound effects; Cobra King attack hisses play
  as each attack strikes
- gold armor intro words show only the first time it is gained
- Silk Gloves no longer worn on the monkey; their effect is unchanged
- first-grab popup points at the nearest visible fruit
- falling cobras much lighter, so the climb after the Cobra King no longer
  stutters
- a double-tap in a boss fight no longer throws a coconut

##### Erik

- scripts refactored to the `u3d` conventions; 11 scripts renamed to role
  suffixes, prefabs keep their scripts
- cobras, bosses and boss HUDs read tuned values from Config assets; values
  unchanged

### Fixed

##### Belle Dai

- nickname field no longer triggers IME composition; hovering buttons no longer
  steals text focus

##### Wenhai Dong

- boss fights no longer log collider, `BoxCollider` and `ArgumentException`
  errors

[1.0.0-beta.5]: https://github.com/kami-lel/monkobra/compare/v1.0.0-beta.4...v1.0.0-beta.5













## [1.0.0-beta.4] - 2026-11-17

### Added

##### Erik

- white always-on outline on the skinned monkey, matching grabbable fruit
- hack layer: F12 (development build only) toggles hack keys & key list, off at start; release builds keep keys live
- analytics records the Spider King fight: start, revive & win; mock session plays a spider fight after a won Cobra King one
- Spider King parry lesson shows the animated double tap icon, replacing the "Tap SPACE 2 times" text
- accent fonts back over the Baloo 2 default: BadaBoom on scores, Chewy on level-select & Download Analytic labels, Life Is Goofy on the Endless unlock hint

##### Yuqing Lu

- WebGL loading page: cobra eats a row of dots along a branch on real download progress, monkey hopping ahead; ends on the title picture & fades into the game
- team logo screen before loading: USC Games & Peel Five; click, key or same-tab reload skips it
- title screen redrawn: logo, grown cobra & monkey on a branch, vines
- UI style from the logo (`docs/ui-style-doc.md`): Baloo 2 font, palette, logo-style `MenuButton` & `IconButton` prefabs, keycap key icons, applied by `Monkobra > UI Style` menus

##### Houdong Pan

- skinned Mr. Mo monkey model replaces the capsule one, visual only: colliders & grab judging unchanged; credited in `CREDITS.md`, see `docs/monkey-model-doc.md`
- procedural climb (`MonkeyModelAnimator`): diagonal gait paced by real speed, backward on descent, still when stopped; hip sway, spine roll, breathing, spring tail, a look back over the shoulder, more often when the great cobra is close; stretched arms keep their thickness
- arm IK (`MonkeyModelArms`): hands follow the climb cycle, blend onto the physics hands when reaching or leaping so the visible grab lands where it is judged
- background music (`MusicManager`): a ukulele & djembe climb loop, crossfading to a faster, tenser drum loop during a boss fight & back; kept across restarts, silenced by pause; both made from sines & noise by `tools/music/make_music.py`

##### Wenhai Dong

- second boss, the Spider King (`BossSpiderKing/`), at 1000 u in a bare arena w/ a web cap: crawls round the tree above the monkey, spinning webs along its way; killed by thrown melons (15 health) in 3 stages (volleys of 1~3 web bullets, slash rows of 1~3, dashes along a red lane), every attack telegraphed
- Spider King parry: double tap Space as an attack turns gold; a parry hurts it, plays a slow motion clash w/ its own camera shot & is taught by 2 stop time lessons w/ the animated double tap icon
- Spider King armor: starts w/ the gold armor on, a pair of golden melons gives it back; a hit it saves throws the monkey clear; touching it hurts only through its attacks
- Spider King win: Silk Gloves, webs break 2x faster, after the reward panel; EASY / NORMAL revive as the Cobra King's
- Spider King UI: call outs for rapid & triple fire, stage banners & health bar in the UI style
- monkey 1.2x as fast in the Spider King's arena (`MonkeyController.ArenaSpeedScale`)
- boss height markers: the Cobra King coils round its marker's plate, the Spider King's 1,000 m marker has a spider perched on its red beating plate (`BossMarkerFigure`)
- hack key 2 jumps to just below the Spider King

##### Belle Dai

- cartoon snakes replace the great cobra's head & the falling cobra's body: smooth attacks, a proximity hiss along the whole body (`CartoonCobraVisual`, `FallingSnakeSurface` shader)
- cobra hiss plays as the chase introduction camera cuts in

### Changed

##### Erik

- project layout follows the u3d skills: renamed & re-homed folders, `Boss/` split into `BossCobraKing/` & `BossSpiderKing/` (classes renamed `CobraKing*`, `SpiderKing*`)
- 1 gameplay scene: run mode passed on load, `EndlessScene` gone
- 1 prefab = 1 entity: group prefabs dissolved into the scene; 11 popup variants → 1 `PopUpCatalog`; 9 input icons → 2 + `HintIconLibrary`; branch prefabs share `_BranchBase`
- script conventions applied project-wide; hack keys read through `HackShortcutConfig`
- god classes split by role, behavior unchanged: falling cobra, great cobra motor, Cobra King fight, boss HUD, Spider King fight
- hot paths cached; dormant falling cobras skip work far from the monkey
- ramped difficulty in 3 stages, 1 per boss; Endless opens at the final stage; hazard & cobra curves re-keyed to 0~3
- Spider King fight area moved down 50 u: trigger 1000, height marker 1000 m
- hack keys regrouped: F1~F3 cheats, 1 & 2 boss jumps, 3~6 spawns, F8~F10 analytics, F12 switch (was F9)
- analytics titles tell bosses apart: "Cobra Fight Start" & "Cobra Revive"
- PC render pipeline: opaque texture off

##### Yuqing Lu

- pause & lose screens: Exit returns to level selection, not title
- Made with Unity splash screen off
- level selection, in-game HUD, pause & lose screens, boss reward panel & revive card in the UI style
- title start prompt & loading page "Loading" in Baloo 2, keeping 1 font through the hand-over
- hack key list hidden at start, F9 shows or hides it

### Removed

##### Erik

- dead assets: `_Banana.mat`, `SlipperyTutorialBand.asset`, `FreeGUI-Button.png`, `RoughBark_Albedo/_Normal.png`, empty `Assets/Resources/`

##### Yuqing Lu

- tutorial Noto Sans TC subset font & old popup plate sprite, replaced by Baloo 2

### Fixed

##### Erik

- guaranteed first coconut & pineapple grow on the monkey's side of the trunk, never the far side
- spider web catches only the monkey's root capsule inside a round zone fitted to the web, no longer tail & hands
- no falling cobra hangs without a branch after the Cobra King fight

##### Yuqing Lu

- lose screen: Download Analytic button fits its words, key hint row fits at 4:3, Restart & Exit words back
- progress bar: faces no longer squashed, cobra stays inside the bar & fills it from the bottom

[1.0.0-beta.4]: https://github.com/kami-lel/monkobra/compare/v1.0.0-beta.3...v1.0.0-beta.4













## [1.0.0-beta.3] - 2026-11-11

### Added

##### Erik

- touch screen play: the first touch switches the game to touch mode, a real key press switches it back (`ControlStyleManager`, kept across scenes); in touch mode a joystick bottom left moves the monkey and the rest of the screen is the Space key (hold to reach, double-tap for a coconut, tap rapidly to escape a web, tap to start)
- phones & tablets, mobile browsers incl., start in touch mode, so the first tap counts; the WebGL page leaves touch gestures to the game
- virtual joystick sprites by rmeldev (CC0), credited in `CREDITS.md`
- pause button, top right, by click or tap, a square icon button; in Keyboard style the Esc key icon & "to Pause" sit beside it
- animated hint icons, one per control style (`HintIconSlot`), in the tutorial, popups, coconut hint & boss prompt; the touch tutorial shows a blue action area over the touch zone
- menu buttons from the FreeGUI sheet by pzUH (CC0), credited in `CREDITS.md`: blue, green on hover or key selection, red on press, grey when locked
- pause screen: square Resume & Restart buttons, with the Esc & R key icons in Keyboard style
- Exit to Title is the square `HUDExit_*` icon button on the pause & lose screens, and the lose screen's Restart is the square `HUDRestart_*` button with the R key icon in Keyboard style
- pause & lose screens: the "W / S to select, Space to confirm" text is the level select's `LevelSelectHint_Keyboard` / `_Touch` icon hints, switched by control style
- pause & lose screens: each option is a 3-column row, key icon, square button, text label (Resume, Restart, Exit); the key column hides in Touch style
- every key hint (tutorial, pause, lose & level select screens, coconut how-to, popups, keycaps, boss buttons) follows the control style live
- E & Enter join Space on every confirm: grab, coconut, web escape, tutorial start, menus, title & boss panels; P backs out of level select like Esc
- `PersistentBootstrap` creates `Analytics` & `ControlStyleManager` from `Resources/` before the first scene, so any scene plays with both; scene copies stay, the duplicate warning is expected
- [Input](docs/input-doc.md): the control scheme, Preferred vs Support keys per control style

### Changed

##### Erik

- title starts on Space, E, Enter, a click or a tap, no longer on any key
- boss revive, ad skip & reward panels read input actions, not raw keys; the course page is button only, Enter no longer opens it
- hints teach the Preferred input only: icons replace key names, and P, arrows, E & Enter are never drawn

### Deprecated

### Removed

##### Erik

- "Esc or P to Pause" label, the pause screen's key text & the keycap icon table (`Scripts/InputIcons/*`)

### Fixed

##### Erik

- HUD graphics & the tutorial overlay swallowed taps meant for the touch zone

### Security

[1.0.0-beta.3]: https://github.com/kami-lel/monkobra/compare/v1.0.0-beta.2...v1.0.0-beta.3













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
