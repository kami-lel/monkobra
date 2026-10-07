# Monkobra

A 3D vertical climbing game: scale a very tall tree while a cobra chases you from below. *Raiden* meets *Gold Miner*, in 3D.

Course project for USC CSCI-526, Fall 2026.

## Overview

You play a monkey racing up a very tall tree. The cobra never stops climbing, and your stamina drains with every meter. Dodge branches, then hold Space to reach for a banana and release to grab it: a clean grab is the only way to refill the stamina bar before it runs dry. Every grab costs focus that would otherwise go to dodging, so each banana is a risk. Genre, design pillars, and the reasoning behind the twist are in the [Game Design Document](docs/monkobra-gdd.md).

### Controls

| Input | Action |
| --- | --- |
| Up, Down | climb the tree |
| Left, Right | circle around the tree |
| Space (hold and release) | reach out and grab a banana |
| Space (press rapidly) | break free of a spider web |

## Mechanics

- Movement and Climb: free vertical & trunk-circling movement, climbing costs most stamina, circling less, descending none
- Cobra Chase: cobra coils up the trunk from below, any contact ends the run
- Falling Cobra (demo scene): after a set climb distance, snakes warn from branches before falling, more often at new heights. Contact shakes the camera & knocks the monkey down
- Branch Collision: a hit shakes the camera, drops the monkey, briefly removes control
- Banana Grab: hold to stretch the arm, release to grab, a clean grab restores stamina
- Stamina Bar: drains over time & with movement, refilled only by grabbing bananas
- Score: points for each new meter of height & each banana grabbed, total shows on screen & on the win or lose panel
- Spider Web: webs cling to the upper half of the trunk. Touching one holds the monkey until Space is mashed to fill the escape bar, then the web vanishes & the monkey is briefly immune

## Getting Started

Open the repository folder through Unity Hub with Editor `6000.3.22f1` (Unity 6, URP), then open `Lv1Scene` and press Play. The game also runs in the browser from the committed WebGL export in `Monkobra/`, served by GitHub Pages.

### Testing the Spider Web

Webs are part of `Lv1Scene`; `WebDemo` is a smaller test scene with the same web setup and no score. Open either and press Play. Webs only spawn from the middle of the tree up, so for a quick test select the `Tree` object and set `SpiderWebSpawner` > Start Progress to `0` (and Spawn Chance to `1`) to put webs right by the start, then leave the scene unsaved. With Gizmos on, yellow boxes mark each web's trigger and a cyan ring marks the start height. The first trap shows the hint text and bar, later traps the bar only, and reloading the scene brings the hint back.

## Contributing

Agent rules and the WebGL export steps are in [AGENTS.md](AGENTS.md), the system map and known gaps in [CONTEXT.md](CONTEXT.md), the design intent in the [Game Design Document](docs/monkobra-gdd.md), and version history in [CHANGELOG.md](CHANGELOG.md).

## Acknowledgments

Team: Yuqing Lu, Yangyi Lu (Erik), Houdong Pan, Wenhai Dong, Belle Dai.
