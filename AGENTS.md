# Monkobra AGENTS

Unity course project (USC CSCI-526, Fall 2026): a 3D vertical climbing game. Overview in [README.md](README.md), system knowledge in [CONTEXT.md](CONTEXT.md). Chat instructions override this file.

## Layout & Naming

- The repository root is the Unity project root: `Assets/`, `Packages/`, and `ProjectSettings/` sit at the top level
- Authored assets live under `Assets/_Monkobra/`, laid out `<Type>/<Module>/<asset>` per the `coder-unity-engine` skill (e.g. `Scripts/`, `Prefabs/`, `Scenes/`, `Settings/_Shared/`)
- Unity template leftovers (`Assets/Readme.asset`) stay outside the game root
- Every asset under `Assets/` is committed with its `.meta` file, and a moved or renamed asset moves its `.meta` with it
- Never commit `Library/`, `Temp/`, `Obj/`, `Build*/`, `Logs/`, or `UserSettings/`: all are already in `.gitignore`

## Setup Commands

Editor version is Unity `6000.3.22f1` (Unity 6, URP). Open the repository folder through Unity Hub. No build or test command exists yet.

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
