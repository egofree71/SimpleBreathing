# SimpleBreathing — Current Implementation

This document describes the current implementation of the SimpleBreathing app.

The goal is to make it easy to resume work in a new discussion by explaining:
- the general architecture,
- the folder structure,
- the scenes,
- the scripts,
- the current behavior,
- the planned features,
- and the known limitations.

## 1. Project Goal

SimpleBreathing is a very simple Android breathing app made with Godot.

The main screen displays a vertical breathing gauge with a moving ball.

The ball moves:
- upward during inhalation,
- downward during exhalation.

The user should eventually be able to:
- change the inhalation duration,
- change the exhalation duration,
- customize the colors,
- pause/resume the breathing cycle,
- save preferences locally.

## 2. Technology

- Engine: Godot 4.x .NET
- Language: C#
- Target platform: Android
- Secondary target: Windows desktop for testing

## 3. Folder Structure

```text
SimpleBreathing/
├── assets/      # Images, icons, fonts, sounds, visual resources
├── docs/        # Project documentation
├── scenes/      # Godot scenes
├── scripts/     # C# scripts
├── README.md    # Short public project description
├── .gitignore   # Files ignored by Git
└── project.godot
```

## 4. Scenes

### Main.tscn

Main application scene.

Planned responsibilities:
- display the background,
- display the vertical breathing gauge,
- display the breathing ball,
- display the current breathing phase: `Inhale` / `Exhale`,
- handle the breathing animation,
- later provide access to settings.

Possible node structure:

```text
Main
├── Background
├── Gauge
├── BreathingBall
├── PhaseLabel
└── SettingsButton
```

This structure may change as the project evolves.

## 5. Scripts

### BreathingController.cs

Main script controlling the breathing cycle.

Responsibilities:
- keep track of elapsed time,
- calculate the current breathing phase,
- move the ball up and down,
- apply smooth easing to the movement,
- expose inhale/exhale durations,
- later handle pause/resume.

Basic algorithm:

```text
cycleDuration = inhaleDuration + exhaleDuration
phaseTime = elapsedTime % cycleDuration

if phaseTime < inhaleDuration:
    phase = Inhale
    t = phaseTime / inhaleDuration
    ball moves from bottom to top
else:
    phase = Exhale
    t = (phaseTime - inhaleDuration) / exhaleDuration
    ball moves from top to bottom
```

The movement should use easing so that the ball moves gently instead of mechanically.

Example easing idea:

```csharp
private float Smooth(float t)
{
    return 0.5f - 0.5f * Mathf.Cos(t * Mathf.Pi);
}
```

## 6. Breathing Behavior

Initial default values:

```text
Inhale duration: 5 seconds
Exhale duration: 5 seconds
```

The ball starts at the bottom of the gauge.

During inhalation:
- the ball moves upward.

During exhalation:
- the ball moves downward.

The cycle repeats indefinitely.

## 7. Visual Style

Initial visual idea:
- dark blue background,
- vertical rounded gauge,
- glowing breathing ball,
- minimal interface,
- calm and non-distracting look.

Reference image:

```text
assets/respiration_reference.png
```

## 8. Settings — Planned

Planned settings:

```text
Inhale duration
Exhale duration
Background color
Gauge color
Ball color
```

These settings should eventually be saved locally.

Possible Godot storage method:
- `ConfigFile`,
- or a custom `Resource` file,
- or a simple JSON file.

## 9. Android Export

The app is intended for personal Android use.

First goal:
- export an APK,
- install it manually on the phone,
- test the animation and screen scaling.

Publishing on the Play Store is not required for the initial version.

## 10. Current Status

Initial repository created on GitHub.

The project has been cloned locally.

Next steps:
1. create the Godot .NET project inside the cloned folder,
2. add the initial folder structure,
3. add this documentation file,
4. create the first `Main` scene,
5. implement the basic breathing ball animation.

## 11. Known Questions / Decisions

Open questions:
- Should the UI be made with `Control` nodes or `Node2D`?
- Should the gauge and ball be drawn with Godot shapes or with custom textures?
- Should the first version support only portrait orientation?
- Should settings be on the same screen or in a separate scene?

Initial decisions:
- Keep the first version extremely simple.
- Prioritize a working breathing animation before adding settings.
- Use Git commits after each stable milestone.
- Update this document whenever the architecture or behavior changes.
