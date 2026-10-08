# Monkobra CHANGELOG

<!--
TODO data collection system

Fixme clear SO organize
Fixme update per project assets rule
Fixme rename cobra as great cobra

Bug falling cobra gone
-->

## [Unreleased]

### Added

- Spider web trap in `Lv1Scene`, also in the `WebDemo` test scene: webs spawn on the upper trunk clear of branches and fruit, hold the monkey on contact, and break on rapid Space presses, with a first-trap hint and an escape bar that hide on escape or game over, and a short immunity after escaping

- `DTSPool`: recycles the lowest tree segment over the top as the monkey climbs, for an endless tree; deactivates an optional mock tree at runtime

### Changed

- `DynamicTreeSegmentPrefabRoot.Restart` moves a segment and re-rolls its branches, clearing the old branches and webs
- `MonkeyPrefabRoot` exposes `IsHeld` to freeze the body in place
- falling cobra contact in `FallingCobraDemo` now uses the monkey's branch-hit stun and drop instead of ending the run
- falling cobra spawning now unlocks after a configurable climb distance from the monkey's starting height instead of 60% of the finite tree
- falling cobra spawn intervals now shorten with the monkey's highest climb, down to a configurable minimum
- cobra coil fill: `CobraClimb` clones body segments so the coil has no gap
- cobra look: bead-chain body w/ small gaps & slight taper, flat hooded head w/ eyes, raised neck, slow slither wave
- cobra body coils ≥ 1 full loop round the trunk as a corkscrew, so dropping onto it or orbiting into it is always fatal
- cobra coil shape tuned by `coilTurns`, `coilPitchU`, `maxSegmentGapU`, replacing per-segment angle & height spacing
- `Cobra/` scripts → house Unity style

### Deprecated

### Removed

### Fixed

- cobra contact script class renamed `CobraCollisions` to match its file, so Unity can load it
- cobra contact counts the monkey's tail & hands, not only its body; detection triggers no longer matter
- cobra no longer retreats downward with a descending monkey, which let the monkey push it down and never touch it
- cobra contact also checked while overlapping, not only on entry
- monkey body collider height 1 → 2 to match its visible body, so contact registers from the monkey's bottom, not its middle (also reaches win zone & other triggers ~0.5 U sooner)
- cobra no longer parks at the player's height waiting for its head to come round; the coil climbs on into the player, so body contact kills at once

### Security

[unreleased]: https://github.com/CSCI-526/Team4-Lu_Lu/compare/v0.1.1...dev



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

[0.1.1]: https://github.com/CSCI-526/Team4-Lu_Lu/compare/v0.1.0...v0.1.1



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

[0.1.0]: https://github.com/CSCI-526/Team4-Lu_Lu/releases/tag/v0.1.0
