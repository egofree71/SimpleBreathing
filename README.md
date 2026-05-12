# SimpleBreathing

A very simple Android breathing app built with **Godot 4.6.2** and **C#**.

## Goal

SimpleBreathing displays a vertical breathing gauge with a ball that moves upward during inhalation and downward during exhalation.

The interface is intentionally minimal and calm:

- the main screen focuses on the gauge and the breathing ball;
- the gauge is vertically centered in the screen;
- settings are kept on a separate screen;
- during a breathing session, controls are hidden;
- tapping the screen pauses the session;
- while paused, the app shows elapsed session time and a progress bar;
- when a session ends naturally, the app uses a soft fade transition and displays a completion message;
- while a breathing session is active, the phone screen is kept awake so the rhythm remains visible.

## Current state

Implemented:

- minimal Godot C# project;
- main scene configured for a mobile portrait layout;
- main UI built in C#;
- vertical rounded capsule-shaped gauge;
- large breathing ball inside the gauge;
- eased ball movement for a more natural breathing rhythm;
- configurable inhalation and exhalation durations;
- configurable total session duration in whole minutes;
- start, pause, resume, stop, and soft automatic session completion flow;
- pause progress display above the gauge;
- separate settings screen;
- immediate settings application and auto-save;
- settings persistence through Godot `user://settings.cfg`;
- selectable visual themes;
- neutral black-and-white settings screen for readability;
- basic localization in English, French, and Spanish;
- completion overlay with fade out / fade in and localized completion message;
- Android/mobile screen kept awake while a breathing session is active;
- documented `Main.cs` controller methods.

## Technical documentation

See:

```text
docs/current_implementation.md
```

This file describes the current architecture, scenes, scripts, UI behavior, and implementation details.
