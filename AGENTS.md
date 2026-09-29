# Monkobra AGENTS

Unity course project (USC CSCI-526, Fall 2026): a 3D vertical climbing game. Overview in [README.md](README.md), system knowledge in [CONTEXT.md](CONTEXT.md). Chat instructions override this file.

## Layout & Naming

- The repository root is the Unity project root: `Assets/`, `Packages/`, and `ProjectSettings/` sit at the top level
- Authored assets live under `Assets/_Monkobra/`, laid out `<Type>/<Module>/<asset>` per the `coder-unity-engine` skill (e.g. `Scripts/`, `Prefabs/`, `Scenes/`, `Settings/_Shared/`)
- Unity template leftovers (`Assets/Readme.asset`) stay outside the game root
- Every asset under `Assets/` is committed with its `.meta` file, and a moved or renamed asset moves its `.meta` with it
- Never commit `Library/`, `Temp/`, `Obj/`, `Build*/`, `Logs/`, or `UserSettings/`: all are already in `.gitignore`

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
  `TemplateData/`, and `StreamingAssets/` (if used); this name was
  chosen specifically to avoid `.gitignore`'s `Build*/` rule (see
  Layout & Naming above), which would otherwise silently drop a
  committed export
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

- Work happens on `dev`, and `main` is the merge target
- Commits follow the user's global git rules: run the `prepare-commit-msg` hook, never pass `-m`

## Documentation Maintenance

- Update [CONTEXT.md](CONTEXT.md) in the same change whenever entities, systems, or mechanics shift
- Update [README.md](README.md) when controls, setup steps, or the mechanic list change
- Update this file when commands or conventions change
