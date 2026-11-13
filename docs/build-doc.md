# MonKobra: Build Documentation

Unity `6000.3.22f1`. Every build uses its own Build Profile in `Assets/Settings/Build Profiles/` and goes to `builds/Monkobra v<version>+<Target>`, version from Player Settings. The profiles carry no Player Settings override, so every build reads the project's Player Settings and the version needs bumping only there; keep it that way, since a profile with its own override keeps a stale copy (WebGL-only values such as the exception support, IL2CPP code generation, and stripping level sit in the global Player Settings under their WebGL keys). No build is committed; `builds/` stays gitignored. Build only from the profile; change a setting in the profile, never per build.

## GitHub Pages

Profile: `GithubPages` (WebGL)
Location: `builds/Monkobra v1.0.0-beta.3+GitHub`

Profile settings (Pages cannot set response headers):

- Compression Format: `Disabled`; a `Gzip` or `Brotli` build fails to load
- Decompression Fallback: off; tick it only if size forces compression back on
- WebGL Template: `FitWindow` (`Assets/WebGLTemplates/FitWindow/`): keeps the game at 16:9 and scales it to the largest box the browser window allows, black bars filling the rest, rendering at the screen's real pixel density; a Fullscreen button sits at the bottom right. For touch play it sets `touch-action: none`, switches off text selection and the tap highlight, and blocks the long-press context menu, so no drag scrolls or zooms the page and a held finger (the grab) stays with the game. It reaches a build only on the next export; never edit `Monkobra/` by hand. Set `MAX_RENDER_HEIGHT` in its `index.html` to cap the render resolution if frame rate drops
- Development Build: on, since the hack hints and dev-only UI need it ([Dev Keys](#dev-keys)); the cost is a larger, slower export
- Default Canvas: 1280×720 (720p), only the starting size, since `FitWindow` resizes the canvas to the window; the `FullPlayScene` Canvas Scaler uses the same 1280×720 reference, so the UI lays out the same at any size
- Scenes: the global list, in order `TitleScene` (the startup scene), `LevelSelectScene`, `FullPlayScene`, `EndlessScene`

Phones: iOS browsers (all WebKit) have no Fullscreen API for a canvas and cannot lock the orientation. The `FitWindow` template therefore hides the Fullscreen button there and shows an Add to Home Screen hint, links `manifest.webmanifest` for a chrome-less launch from the home screen, and, on a touch phone held in portrait, covers the page with a rotate message and calls `GameController.PauseFromBrowser`, so a live run waits behind the pause screen

Steps:

1. `File > Build Profiles`, select `GithubPages`, click Build, choose the location
2. Test, since WebGL will not run from `file://`:

```bash
python3 -m http.server 8000 --directory "builds/Monkobra v1.0.0-beta.3+GitHub"
```

## itch.io

Profile: `ItchIO` (WebGL)
Location: `builds/Monkobra v1.0.0-beta.3+Itch`

Profile settings: same as GitHub Pages, Development Build on included (`Disabled` works; gzip is the fallback, avoid Brotli).

Steps:

1. `File > Build Profiles`, select `ItchIO`, click Build, choose the location
2. Zip the folder's contents, so `index.html` sits at the zip root
3. Page settings: Kind of project `HTML`, tick "played in the browser"
4. Embed options: "Embed in page" with a manual size of 1280×720; `FitWindow` fits the game into whatever frame size is set
5. Frame options: tick only "Fullscreen button"
6. Set visibility to Draft while testing, then Public
7. Re-upload: upload the new zip, delete the old one
8. Check: load in a private window, watch the devtools Network tab

Limits: 1,000 files, 240 characters per path, 500 MB total, 200 MB per file.

## Dev Keys

The hack keys (F1 no-clip, F2 max energy, F3 speed buff, F4 boss jump, F5~F8 spawns, F9 spider boss jump, F10 report download, F11 and F12 analytics, see [Hack Shortcuts](monkobra-tdd.md#hack-shortcuts)) run only when `Debug.isDebugBuild` is true, so the WebGL profiles (`GithubPages`, `ItchIO`) keep Development Build on and a player can press them. A build with it off, e.g. Windows by default, has none of them. The browser needs focus on the game canvas when F10 is pressed; F11 and F12 may also be taken by the browser (full screen, dev tools).

## Windows

Location: `builds/Monkobra v1.0.0-beta.3+Win`

1. `File > Build Profiles`, select Windows, click Build, choose the location
2. Run the `.exe`
3. Zip the whole folder
