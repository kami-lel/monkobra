# Monkobra AGENTS

Prescriptive rules for agents working in this project.

- Belongs Here: rules an agent must follow, ie commands, conventions, constraints, who decides; only what an agent cannot infer from the code
- Size: stay lean for the context window; when the file grows, move detail to `docs/` or cut it
- Upkeep: update in the same change that moves a command, convention, or constraint
- Structure: follow the existing sections; keep every rule true for every contributor
- Belongs in `CONTEXT.md`: descriptive knowledge of the system, ie architecture, domain model, patterns, known gaps
- Belongs in `docs/`: system documentation for users and agents alike; link it from here, never grow this file to hold it
- Local Layer: put machine-specific or personal rules in `AGENTS.local.md`; create it if missing, never commit it

Unity course project (USC CSCI-526, Fall 2026). Overview in [README.md](README.md), system knowledge in [CONTEXT.md](CONTEXT.md), game design intent in the [Game Design Document](docs/monkobra-gdd.md). Chat instructions override this file.

Read these `docs/` files when their topic arises:

- [Technical Design Document](docs/monkobra-tdd.md): engine, run lifecycle, monkey movement, tree, stamina, score, input, UI, and script conventions; start here for anything not covered below
- [Mobs](docs/mob-doc.md): the cobra, falling cobra, branches, and spider webs, and what each does when the monkey touches it
- [Grab System](docs/grab-doc.md): detection zones, arm reach, the release check, the camera swap, and fruit outlines

## Layout & Naming

- The repository root is the Unity project root: `Assets/`, `Packages/`, and `ProjectSettings/` sit at the top level
- Authored assets live under `Assets/_Monkobra/`, laid out `<Type>/<Module>/<asset>` per the `coder-unity-engine` skill (e.g. `Scripts/`, `Prefabs/`, `Scenes/`, `Settings/_Shared/`)
- Unity template leftovers and imports (`Assets/Readme.asset`, `Assets/TextMesh Pro/`) stay outside the game root
- Every asset under `Assets/` is committed with its `.meta` file, and a moved or renamed asset moves its `.meta` with it
- Never commit `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, or `UserSettings/` at the repository root: all are already in `.gitignore`

## Setup Commands

Editor version is Unity `6000.3.22f1` (Unity 6, URP). Open the repository folder through Unity Hub. The only build is the WebGL export below, made from the Editor; no test command exists yet.

## GitHub Pages WebGL Build

Method adapted from the sibling repo
`usc-csci526-asgn1-u3d-creative-core-learn`, which serves its WebGL
export straight off GitHub Pages; no GitHub-side build step runs
either there or here, the committed export is served as-is:

- the WebGL export lives in `Monkobra/`, directly under the repo
  root (sibling to `Assets/`, never inside it), a complete,
  self-contained export holding `index.html`, `Build/`,
  `TemplateData/`, and `StreamingAssets/` (if used); never name it
  `Build/` or `Builds/`, which `.gitignore` ignores at the repo root
  (see Layout & Naming above) and would silently drop a committed
  export
- export via `File > Build Settings > WebGL > Build`, output
  directly into `Monkobra/`
- serve locally before pushing (WebGL will not run from `file://`):
  `python3 -m http.server 8000 --directory Monkobra`
- commit and push `Monkobra/`; `Settings > Pages > Deploy from
  branch` (root of `main`) then serves it automatically
- never hand-edit a file inside `Monkobra/`; re-export from Unity
  and re-commit instead

Build Profile / Player Settings for the WebGL target, required
because GitHub Pages is plain static hosting with no control over
response headers:

- Player Settings > Publishing Settings > Compression Format:
  `Disabled`; GitHub Pages cannot set the `Content-Encoding` header
  a `Gzip`/`Brotli` export needs, so a compressed build fails to
  load. If build size later forces compression back on, also tick
  Decompression Fallback so the page unpacks itself in the browser
- Player Settings > Publishing Settings > WebGL Template: `Default`,
  unless a later change intentionally swaps it for a custom shell
- Build Profile (`File > Build Profiles > WebGL`): only the intended
  scene(s) checked under Scenes in Build; Development Build off for
  the exported version that gets committed and pushed
- Player Settings > Resolution and Presentation: keep the default
  canvas set to stretch/fill so the game fits whatever `index.html`
  embeds it in

## Code Style

- Unity code follows the `coder-unity-engine` skill conventions
- Keep gameplay tuning values (stamina drain, grab restore, cobra speed) in serialized fields, never hard-coded

## Testing Instructions

No test suite exists yet. When one is added, record the exact scoped command here, and keep tests in sync with changed code.

## PR & Commit Instructions

- Feature work always happens on a feature branch, never directly on `dev` or `main`
- Before starting feature work on `dev` or `main`, ask the user to create or switch to a feature branch, and wait for the answer
- Feature branches merge into `dev`, and `main` is the merge target
- Do not use the `prepare-commit-msg` hook: write the commit message yourself and pass it with `-m`

## Documentation Maintenance

- Update [CONTEXT.md](CONTEXT.md) in the same change whenever entities, systems, or mechanics shift
- Update [README.md](README.md) when controls, setup steps, or the mechanic list change
- Update the [Game Design Document](docs/monkobra-gdd.md) when design intent shifts, ie pillars, the core twist, resources, or win and lose rules; read it before changing how a mechanic is meant to feel
- Update [Mobs](docs/mob-doc.md) when a cobra, branch, or web changes how it spawns, moves, or reacts to a touch, and the [Grab System](docs/grab-doc.md) when detection, reach, or release rules change
- Update the [Technical Design Document](docs/monkobra-tdd.md) when the architecture, run lifecycle, tuning assets, input bindings, or conventions change
- Update this file when commands or conventions change
