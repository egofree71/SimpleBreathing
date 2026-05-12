# SimpleBreathing

A very simple Android breathing app built with **Godot 4.6.2** and **GDScript**.

The app displays a vertical breathing gauge with a ball that moves upward during inhalation and downward during exhalation. The goal is to keep the experience minimal, calm, and easy to use on a phone.

## Current state

Implemented:

- Godot **standard / non-.NET** project;
- GDScript runtime implementation;
- portrait mobile layout;
- main screen focused on the breathing gauge;
- separate settings screen;
- vertical rounded capsule-shaped gauge;
- large breathing ball inside the gauge;
- eased ball movement for a smoother breathing rhythm;
- configurable inhalation and exhalation durations;
- configurable total session duration in whole minutes;
- start, pause, resume, stop, and automatic completion flow;
- pause progress display above the gauge;
- immediate settings application and auto-save;
- settings persistence through `user://settings.cfg`;
- selectable visual themes;
- neutral black-and-white settings screen for readability;
- SVG icons for the back, stop, and play controls;
- basic localization in English, French, and Spanish;
- completion overlay with fade transition and localized completion message;
- Android/mobile screen kept awake while a breathing session is active;
- Android navigation bar kept visible, with edge-to-edge safe-area handling.

The project was originally implemented in C#/.NET, but the active app logic has been migrated to GDScript. The project no longer needs Godot .NET or the .NET SDK for normal desktop testing or Android export.

## Requirements

Recommended setup:

- Godot **4.6.2 stable**, standard non-.NET version;
- Godot Android export templates for exactly the same Godot version;
- Android SDK;
- Android SDK Platform-Tools;
- Android SDK Build-Tools;
- Android SDK Command-line Tools;
- Android NDK;
- CMake;
- OpenJDK.

The project should not require:

- Godot .NET;
- a C# project file;
- .NET SDK 8 or 9;
- MSBuild for Android export.

## Android export notes

Recommended Android export settings:

```text
Project > Export > Android > Options > Screen

Immersive Mode: Off
Edge to Edge: On
```

With this setup, the Android navigation bar remains visible while the app background can extend behind translucent system bars. The UI is adjusted using the Android safe area so buttons and sliders are not placed under system bars.

Detailed Android notes are available in:

```text
docs/android_export_notes.md
```

## Technical documentation

See:

```text
docs/current_implementation.md
docs/android_export_notes.md
```

`docs/current_implementation.md` describes the current architecture, scripts, UI behavior, session state, settings persistence, and Android-related runtime behavior.

`docs/android_export_notes.md` documents Android-specific export requirements, export templates, system bar settings, safe-area handling, and boot splash notes.
