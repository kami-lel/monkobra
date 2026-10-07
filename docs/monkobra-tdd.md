# Monkobra Technical Design Document

How Monkobra is built: the engine and packages, the run lifecycle, the systems that do not belong to one subsystem document, the conventions every script follows, and where to find the rest. Design intent is in the [Game Design Document](monkobra-gdd.md), controls and setup in the [README](../README.md), and the agent-facing system map in [CONTEXT.md](../CONTEXT.md).

## Document Map

| Document | Owns |
| --- | --- |
| [Game Design Document](monkobra-gdd.md) | what the player does and why: pillars, loop, mechanics, difficulty |
| This document | engine, architecture, run lifecycle, monkey, tree, stamina, score, UI, input, conventions |
| [Mobs](mob-doc.md) | cobra, falling cobra, branches, spider webs, and what each does when the monkey touches it |
| [Grab System](grab-doc.md) | detection zones, arm reach, release check, camera swap, outline feedback |
| [AGENTS.md](../AGENTS.md) | rules for agents, including the WebGL export steps |
| [CONTEXT.md](../CONTEXT.md) | descriptive system map and known gaps |
| [CHANGELOG.md](../CHANGELOG.md) | version history |

A system gets its own document when it spans several scripts and carries rules a reader cannot see from one file. Everything else stays here.

## Stack

| Aspect | Value |
| --- | --- |
| Engine | Unity 6 (`6000.3.22f1`) |
| Render Pipeline | URP 17.3.0, with a PC and a Mobile asset under `Settings/_Shared/` |
| Input | Input System 1.20.0 |
| Camera | Cinemachine 3.1.7 (view swaps, impulse shake) |
| UI | uGUI 2.0.0 with TextMesh Pro |
| Target | WebGL on GitHub Pages, built from the Editor |
| Tests | none yet, `com.unity.test-framework` is installed |

## Scenes

| Scene | Purpose |
| --- | --- |
| `Lv1Scene` | the game: the only scene in Build Settings |
| `CobraDemo` | test scene for the chasing cobra |
| `FallingCobraDemo` | test scene for the falling cobra, the only scene that has one |
| `WebDemo` | test scene for spider webs, no score |

## Repository Layout

Authored assets live in `Assets/_Monkobra/<Type>/<Module>/<asset>`.

```text
Assets/_Monkobra/
├── Scenes/      the four scenes above
├── Scripts/     ArmRoot, Cobra, Environment, Scoring, UI, GameController, GameConfig
├── Prefabs/     Player, Cobra, Environment, UI
├── Material/    Environment, Player
├── Shaders/     Environment/FruitOutline
└── Settings/    MonkeyConfig, GameConfig, BranchFruitPlacementConfig,
                 Scoring/, _Shared/ (URP assets, input actions)
```

The repository root is the Unity project root. The WebGL export is committed in `Monkobra/`, served as-is by GitHub Pages, and described in [AGENTS.md](../AGENTS.md#github-pages-webgl-build).

## Architecture

```mermaid
graph LR
  Input[Input System] --> Monkey[Monkey: movement, arms]
  Monkey --> Grab[Grab System]
  Grab --> Stamina[StaminaBar]
  Grab --> Score[ScoreManager]
  Mobs[Mobs] -->|touch| Monkey
  Mobs -->|cobra contact| Game[GameController]
  Stamina -->|empty| Game
  Tree[Tree and TreeTop] -->|win zone| Game
  Game --> Screens[ScreensManager]
  Game -->|EndRun| Score
  Config[ScriptableObject configs] --> Monkey
  Config --> Stamina
  Config --> Score
```

Subsystems talk through scene singletons, C# events, and trigger callbacks, not through direct scene references. Details are in [Conventions](#conventions).

## Run Lifecycle

`GameController` is the scene singleton that owns the end state.

1. Start: `Awake` sets `Time.timeScale` to 1 and registers `GameController.I`. `ScreensManager` hides both end panels in `Start`, and `ScoreManager` records the starting height
2. Play: movement, stamina drain, scoring, and the mobs run
3. End: `WinGame` or `LoseGame` runs once. Whichever arrives first sets `IsGameOver`, calls `ScoreManager.EndRun` so the score settles, shows the win or lose panel, and sets `Time.timeScale` to 0

| Outcome | Trigger |
| --- | --- |
| Win | the monkey enters the win zone: `WinZoneHandler` on the `TreeTop` prefab, or `MonkeyPrefabRoot` on a `WinZone`-tagged trigger |
| Lose | the cobra touches the monkey ([Mobs](mob-doc.md#contact)), or stamina reaches 0 |

Gameplay that must stop at the end checks both `Time.timeScale <= 0`, which also covers a pause, and `GameController.IsGameOver`. Current checkers: `ArmRoot.TryGrabFruit`, `ScorePickup`, `ScoreManager.CanScore`, `SpiderWebStruggle`.

## Monkey Movement

`MonkeyPrefabRoot` drives the body and `MonkeyConfig` holds every number.

- Body: kinematic, with gravity off. The script writes position and rotation outright through the Rigidbody, so no arm joint or collision can push it off its path
- Orbit: the horizontal input changes one angle, and the body sits exactly on a ring of fixed radius round its parent, the trunk
- Climb: the vertical input ramps a climb speed along world Y with separate acceleration and deceleration, and different maximums up and down
- Held: `IsHeld` freezes the body in place, set by the web trap ([Mobs](mob-doc.md#trap-and-escape))
- Stun: after a branch hit the input is ignored and the body falls a set distance ([Mobs](mob-doc.md#hit-penalty))
- Stamina Cost: each step reports upward and sideways distance to `StaminaBar.DrainByMovement`. The stun fall and descending cost nothing
- Win: entering a `WinZone`-tagged trigger calls `GameController.WinGame`

| Setting | Value |
| --- | --- |
| Climb Speed Up | 6 u/s |
| Climb Speed Down | 12 u/s |
| Orbit Speed | 50 deg/s |
| Acceleration | 20 u/s² |
| Deceleration | 30 u/s² |

All values come from `Settings/MonkeyConfig.asset`. The arm stroke and reach tuning in the same asset is covered by the [Grab System](grab-doc.md#tuning).

## Tree

The tree is a stack of static and dynamic trunk segments under a `TreeTop`.

- Extent: `TreeManager` (singleton `I`) holds the tree's `MinY` and `MaxY`, 0 and 100 by default, and everything that maps a height to progress reads it: branch density, web start, the progress bar. The tree is finite
- Segments: each `DynamicTreeSegmentPrefabRoot` places branches on itself in `Start` ([Mobs](mob-doc.md#placement)), and `SpiderWebSpawner` places webs after that ([Mobs](mob-doc.md#placement-1))
- Top: the `TreeTop` prefab carries the win zone

## Stamina

`StaminaBar` is a singleton `Slider` that reads `GameConfig`.

- Timer Drain: a fixed amount every interval, as a coroutine started in `Start`
- Movement Drain: `DrainByMovement` per physics step, upward distance costs more than sideways
- Restore: `AddStaminaByFruit` adds the per-banana amount, capped at the maximum ([Grab System](grab-doc.md#on-a-successful-grab))
- Depletion: reaching 0 from either drain calls `LoseGame` once

| Setting | Value | Note |
| --- | --- | --- |
| Max Stamina | 100 | asset |
| Timer Drain | 10 per 10 s | asset |
| Restore Per Banana | 30 | script default |
| Upward Cost | 0.5 per u | script default |
| Sideways Cost | 0.3 per u | script default |

All values live in `Settings/GameConfig.asset`. The fields the asset does not list take their script defaults.

## Score

`ScoreManager` (singleton `I`, on the `GameController` object) keeps two scores and notifies the UI when the total changes.

- Distance Score: whole meters of the highest point climbed times points per meter. Only a new high counts, so falling and re-climbing earns nothing
- Reward Score: points from each banana collected through its `ScorePickup`
- Gate: `CanScore` is true only after start, before `EndRun`, while unpaused, and while the game is not over
- Display: `ScoreDisplay` shows the total on the HUD and on both end panels

| Setting | Value | Asset |
| --- | --- | --- |
| Points Per Meter | 1 | `Settings/Scoring/ScoreConfig.asset` |
| World Units Per Meter | 0.1 | 〃 |
| Points Per Banana | 100 | `Settings/Scoring/BananaScore.asset` |

`WebDemo` and the other test scenes have no `ScoreManager`.

## Camera

Three Cinemachine cameras (behind, look-left, look-right) are swapped by priority, and a shake comes from a Cinemachine impulse source on the branch sensor. The swap rules are in the [Grab System](grab-doc.md#camera), the shake trigger in [Mobs](mob-doc.md#hit-penalty).

## Input

One asset, `Settings/_Shared/InputSystem_Actions`, holds all bindings, referenced through `InputActionReference` fields.

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Move | W, A, S, D or arrow keys | left stick, D-pad |
| Interact | E or Space | north button |
| Jump | Space | south button |

- Move: shared by the body and both arms through `MonkeyConfig`
- Interact: the grab, read by both arms and the camera rig
- Jump: the web escape press, read by `SpiderWebStruggle`
- Collision: Space is bound to both Interact and Jump, so the struggle disables interact while the monkey is trapped

## UI

| Element | Script | Shows |
| --- | --- | --- |
| Stamina Bar | `UI/StaminaBar` | current stamina |
| Climb Progress | `UI/ProgressBarRoot` | the monkey's height as a share of the tree, with a cobra marker |
| Score | `UI/ScoreDisplay` | total score, on the HUD and each end panel |
| End Panels | `UI/ScreensManager` | win and lose panels |
| Web Prompt | `UI/SpiderWebPrompt` | first-trap hint and the escape bar ([Mobs](mob-doc.md#trap-and-escape)) |

## Conventions

- Singletons: scene-scoped and first-wins. `TreeManager`, `UpwardFruitDetection`, `StaminaBar`, and `ScoreManager` destroy a duplicate component only. `GameController` and `ScreensManager` destroy the duplicate's whole GameObject
- Start Over Awake: a consumer that may start before its singleton reads it in `Start`, so Awake order never matters
- Events Over Calls: detection scripts forward trigger events (`Entered`, `Exited`, `BranchHit`) rather than calling their consumers
- Tags: `Branch`, `FruitCollider`, `WinZone`, and `Player` drive trigger matching. Webs match by a Rigidbody component instead
- Tuning In Data: values stay in serialized fields, and shared tuning sits in a ScriptableObject (`MonkeyConfig`, `GameConfig`, `ScoreConfig`, `ScoreReward`, `BranchFruitPlacementConfig`). Gameplay values are never hard-coded
- Field Guards: required inspector fields are checked in `Awake` with a logged message naming the field
- Kinematic Body: nothing about the monkey is mass or gravity driven. Each arm is posed purely by its joint drives
- Style: `ArmRoot/`, `GameController`, `GameConfig`, `UI/StaminaBar`, `UI/ScreensManager`, `Cobra/CobraClimb`, `Cobra/CobraCollisions`, and most of `Environment/` follow the house Unity style. `Scoring/`, `Cobra/FallingCobra`, `Cobra/FallingCobraSpawner`, and `Environment/SpiderWebBuilder` are still in the template style, some with Chinese comments

## Build and Deploy

The only build is the WebGL export made from the Editor into `Monkobra/` and committed. Compression is disabled and the Default template is used, because GitHub Pages cannot set response headers. The steps and the required Player Settings are in [AGENTS.md](../AGENTS.md#github-pages-webgl-build).

## Known Gaps

- No tests, and no run command: verification means opening `Lv1Scene` in the Editor
- The falling cobra is placed only in `FallingCobraDemo`
- Difficulty scaling beyond branch density, web start, and the falling cobra interval is not designed ([Game Design Document](monkobra-gdd.md#difficulty))
- The tree is finite, not endless
- Stamina is not linked to branch hits

The full list is in [CONTEXT.md](../CONTEXT.md#known-gaps--constraints).
