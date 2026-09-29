# Monkobra CHANGELOG

<!--
Todo audio BGM
Todo audio SFX
Todo Outline

fixme cobra done as pool
-->

## [Unreleased]

### Added

- fruit grab: hold interact to lock aim at newest fruit, arm stretches at steady speed, release grabs iff hand overlaps fruit
- fruit grab restores stamina via `StaminaBar.I.AddStaminaByFruit`
- stamina drain by movement: climbing costs most, orbiting less, descent & stun fall free
- `GameConfig` ScriptableObject for game-wide tuning: max stamina, timer drain, fruit restore, climb & orbit drain rates

### Changed

- stamina tuning: `StaminaBar` fields → `GameConfig` asset
- `StaminaBar` exposed as singleton `StaminaBar.I`
- `BranchFruitPlacementConfig` & `GameConfig` assets → `Settings/`
- `ArmRoot`: interact reach replaces spring/overshoot reach, aim locks once at press
- `Arm` joint linear limit 3 → 5
- `Lv1Scene` camera positions, rotations, FOV retuned

### Deprecated

### Removed

- legacy arm drivers `MonkeyArm` & `HandHandler`, superseded by `ArmRoot`
- unused reach tuning from `MonkeyConfig`: drive spring/damper, overshoot, aim weight

### Fixed

### Security

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

[unreleased]: https://github.com/CSCI-526/Team4-Lu_Lu/compare/v0.1.0...dev
[0.1.0]: https://github.com/CSCI-526/Team4-Lu_Lu/releases/tag/v0.1.0
