# Monkobra

A 3D vertical climbing game: scale a very tall tree while a cobra chases you from below. *Raiden* meets *Gold Miner*, in 3D.

Course project for USC CSCI-526, Fall 2026.

## Overview

You play a monkey racing up a very tall tree. The cobra never stops climbing, and your stamina drains with every meter. Dodge branches, then hold E or Space to reach for a banana and release to grab it: a clean grab is the only way to refill the stamina bar before it runs dry. Every grab costs focus that would otherwise go to dodging, so each banana is a risk.

The pillars, mechanics, and difficulty are in the [Game Design Document](docs/monkobra-gdd.md), every hazard and what it does on touch in [Mobs](docs/mob-doc.md), and how grabbing works in the [Grab System](docs/grab-doc.md).

## Controls

| Input | Action |
| --- | --- |
| Up, Down or W, S | climb the tree |
| Left, Right or A, D | circle around the tree |
| E or Space (hold and release) | reach out and grab a banana |
| Space (press rapidly) | break free of a spider web |

Gamepad: left stick or D-pad moves, the north button grabs, the south button breaks free of a web.

## Getting Started

Open the repository folder through Unity Hub with Editor `6000.3.22f1` (Unity 6, URP), then open `Lv1Scene` and press Play. The game also runs in the browser from the committed WebGL export in `Monkobra/`, served by GitHub Pages.

Spider webs first appear 50 u above the monkey's start. To meet them right away, select `Envs/DTSPool` in `Lv1Scene`, tick Spider Web Spawner > Use Test Override before pressing Play, and leave the scene unsaved: webs then spawn from 5 u up on every slot. How often webs appear at each height is tuned in the Spider Web group of `Settings/GameBalanceConfig.asset`. Details are in [Testing](docs/mob-doc.md#testing).

## Contributing

Agent rules and the WebGL export steps are in [AGENTS.md](AGENTS.md), the script index and known gaps in [CONTEXT.md](CONTEXT.md), how the game is built in the [Technical Design Document](docs/monkobra-tdd.md), and version history in [CHANGELOG.md](CHANGELOG.md).

## Acknowledgments

Team: Yuqing Lu, Yangyi Lu (Erik), Houdong Pan, Wenhai Dong, Belle Dai.
