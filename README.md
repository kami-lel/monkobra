# Monkobra

A 3D vertical climbing game: scale a very tall tree while a cobra chases you from below. *Raiden* meets *Gold Miner*, in 3D.

Course project for USC CSCI-526, Fall 2026.

## Overview

You play a monkey racing up an endless tree. The cobra never stops climbing, and your stamina drains with every meter. Dodge branches, then reach out and grab bananas to refill the stamina bar before it runs dry.

- Genre: 3D obstacle-running, in the family of *Temple Run 2*, *Subway Surfers*, and *Minion Rush*
- Engine: Unity
- Camera: monkey held at the center of view, with an infinitely tall tree and a skybox behind

### Core Twist

Banana grabbing and stamina balancing.

- Grab: hold the space key to stretch the monkey's arm toward a nearby banana, release at the right moment to pick it up
- Restore: a successful grab refills the stamina bar by a large margin
- Balance: climbing drains stamina, and grabbing costs focus that would otherwise go to dodging, so every pickup is a risk

### Controls

- Up / Down: climb the tree
- Left / Right: circle around the tree
- Space (hold and release): reach out and grab a banana

## Mechanics

- Movement and Climb: free up-down and around-the-tree movement, with stamina spent on every climb and, at a lower rate, on every step around the trunk
- Cobra Chase: a cobra tangles up the trunk from the bottom of the screen, and touching it ends the run
- Branch Collision: hitting a branch shakes the camera, drops the monkey, and briefly takes away control
- Banana Grab: hold-and-release timing sets how far the arm stretches, and a clean grab restores stamina
- Stamina Bar: drains over time and with movement, topped up only by grabbing bananas

## Getting Started

Open the repository folder through Unity Hub with Editor `6000.3.22f1` (Unity 6, URP), then open `Lv1Scene` and press Play. The game also runs in the browser from the committed WebGL export in `Monkobra/`, served by GitHub Pages.

## Contributing

Agent rules and the WebGL export steps are in [AGENTS.md](AGENTS.md), the system map and known gaps in [CONTEXT.md](CONTEXT.md), and version history in [CHANGELOG.md](CHANGELOG.md).
