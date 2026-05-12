# SimpleBreathing — Current Implementation

## Goal

SimpleBreathing is a very simple Android breathing app built with **Godot 4.6.2** and **GDScript**.

The app displays a vertical gauge with a ball that moves upward during inhalation and downward during exhalation.

The current design goal is to keep the mobile interface calm and minimal:

- the main screen focuses on the breathing animation;
- settings are separated from the breathing screen;
- controls disappear during the breathing session;
- the breathing movement should feel smooth and natural rather than mechanical;
- the gauge should feel visually centered and stable;
- settings changes should be simple and immediate, without a separate save step;
- labels should adapt to the user's language when possible.

## Project structure

```text
SimpleBreathing/
├── project.godot
├── README.md
├── icon.svg
├── assets/
│   └── icons/
│       ├── back.svg
│       ├── play.svg
│       └── stop.svg
├── scenes/
│   └── Main.tscn
├── scripts/
│   ├── main.gd
│   ├── breathing_gauge.gd
│   ├── breathing_settings.gd
│   ├── settings_storage.gd
│   └── app_localization.gd
└── docs/
    ├── current_implementation.md
    └── android_export_notes.md
```

The original C#/.NET implementation has been replaced by GDScript as the active runtime implementation. Any old C# reference files, if still present in a temporary documentation folder, are no longer part of the active Godot project and can be removed after the GDScript version has been validated and committed.

## Project configuration

### `project.godot`

The main scene is:

```text
res://scenes/Main.tscn
```

Current viewport configuration:

```text
width       : 480
height      : 854
orientation : portrait
stretch mode: canvas_items
stretch aspect: expand
```

This matches a phone-oriented vertical layout.

The project is configured as a standard Godot project, not a Godot .NET project. It should be usable with the standard non-.NET editor.

The Godot boot splash image is disabled:

```text
boot_splash/show_image=false
```

### Android system bars

SimpleBreathing should behave more like a small utility app than a fullscreen game on Android. The Android status/navigation system bars should stay visible so the user can leave the app normally with the Home button or gesture area.

For the preferred modern Android look, the navigation/status bars are visible but translucent, and the app background extends behind them. Interactive controls should not be placed under those bars.

In the Android export preset, use:

```text
Options > Screen > Immersive Mode : Off
Options > Screen > Edge to Edge   : On
```

`export_presets.cfg` may be local-only, so this setting may need to be checked manually after cloning the repository.

As a runtime safety net, `main.gd` calls `DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)` on Android. This helps keep the app out of immersive fullscreen mode if a local export preset is wrong.

When Edge to Edge is enabled, `main.gd` also applies safe-area margins through `DisplayServer.get_display_safe_area()`. The full-screen background still fills the whole window, including translucent system-bar areas, but buttons and sliders remain inside the safe area.

## Main scene

### `scenes/Main.tscn`

Root scene type:

```text
Control
```

It uses:

```text
res://scripts/main.gd
```

The scene itself is intentionally minimal. Most UI elements are created in code by `main.gd`, which makes it easier to iterate quickly on mobile layout and behavior.

## Scripts

## `scripts/main.gd`

Main application controller.

Responsibilities:

- build the runtime UI;
- create the main breathing screen;
- create the settings screen;
- create the completion overlay;
- manage screen switching;
- load persisted settings at startup;
- apply and save settings immediately when the user changes them;
- persist settings through `user://settings.cfg`;
- manage the breathing session state;
- manage the inhalation/exhalation cycle;
- manage total session duration;
- keep the phone screen awake while a breathing session is active;
- restore the previous screen sleep behavior when the session stops, completes, or the scene exits;
- update the pause progress display;
- show the completion fade overlay when a session ends naturally;
- apply color themes to the main screen;
- keep the settings screen readable with a neutral black-and-white style;
- keep interactive controls inside mobile safe areas when Android Edge to Edge is enabled;
- use localized labels through `app_localization.gd`.

### Important runtime state

`main.gd` keeps the active settings in:

```text
_settings
```

These settings are used by the main breathing screen and the running session.

The settings screen uses working values:

```text
_draft_inhale_duration
_draft_exhale_duration
_draft_session_duration_minutes
_draft_theme_index
```

Although these variables still use the word `draft`, settings are applied immediately after each user change. There is no explicit save/cancel behavior.

When the user changes a setting, `main.gd`:

1. updates the working value;
2. copies it into `_settings`;
3. writes `_settings` to `user://settings.cfg`;
4. refreshes the UI.

This creates the current mobile-friendly behavior:

```text
change a setting -> apply it immediately -> save it immediately
```

## Main screen

The main screen is intentionally minimal.

Visible at startup:

```text
[⚙]

[gauge + ball]

[play]
```

Details:

- the `Simple Breathing` title is not displayed;
- the settings button is in the top-left corner;
- the start button is centered near the bottom;
- main-screen buttons use a translucent theme-aware style instead of Godot's default dark grey button background;
- the gauge is vertically centered in the screen;
- the top pause-progress area and bottom control area reserve matching heights, so the gauge remains centered and stable;
- the bottom button area is kept stable so the gauge does not resize when buttons appear or disappear.

The start/resume and stop buttons use SVG icons:

```text
assets/icons/play.svg
assets/icons/stop.svg
```

The top settings button still uses the simple gear glyph `⚙`.

### Start

When the start button is pressed:

- the breathing session starts;
- the app asks Godot to keep the phone screen awake;
- the settings button disappears;
- the start button disappears;
- only the gauge and moving ball remain visible.

### Running session

During a running session:

```text
[gauge + moving ball]
```

The screen is visually clean. No buttons are visible.

A transparent full-screen touch area catches taps/clicks. When the user touches/clicks the screen:

- the session is paused;
- the keep-screen-on flag remains active because the session has not been stopped;
- the stop button appears to the left;
- the resume button appears centered under the gauge;
- the pause elapsed-time text is shown above the gauge;
- the pause progress display appears above the gauge.

### Paused session

Visible controls:

```text
[stop]     [play]
```

The resume button stays in the same centered position as the original start button. The stop button appears to its left.

Both bottom buttons are positioned with the same explicit width and height, so the visible button backgrounds remain identical.

Above the gauge, the app displays:

```text
elapsed time / total session duration
[session progress bar]
```

The progress bar:

- is about three quarters of the base viewport width;
- has a semi-transparent white background;
- fills with white according to the elapsed session duration;
- represents progress through the whole breathing session, not only the current inhale/exhale cycle.

Example:

```text
0:42 / 5:00
```

### Stop

When the stop button is pressed:

- the session stops;
- the previous screen sleep behavior is restored;
- the total session timer resets;
- the breathing cycle resets;
- the ball returns to the bottom;
- the app returns to the initial main screen.

### Automatic end

When the configured session duration is reached:

- the session stops automatically;
- the previous screen sleep behavior is restored;
- a full-screen overlay fades the current view to black;
- the app resets the session while the screen is black;
- a localized completion message fades in;
- the message stays visible for about 2 seconds;
- the overlay fades out;
- the app returns to the initial main screen.

The fade transition avoids a sudden visual jump from the final breathing frame back to the start state.

### Completion overlay

The completion message is handled by a full-screen overlay created in `main.gd`.

Overlay elements:

```text
CompletionOverlay
CompletionFade
CompletionLabel
```

The label text is localized through:

```gdscript
AppLocalization.SESSION_COMPLETED
```

Current translations:

```text
English : Session completed
French  : Session terminée
Spanish : Sesión terminada
```

Animation sequence:

```text
fade to black        : about 0.45 s
text fade in         : about 0.35 s
message hold         : 2.0 s
text + black fade out: about 0.45 s
```

The app resets the session while the screen is black, so the user does not see an abrupt jump.

### Screen wake behavior

While a breathing session is active, `main.gd` keeps the device screen awake through:

```gdscript
DisplayServer.screen_set_keep_on(true)
```

Before enabling it, the previous Godot screen wake state is saved with:

```gdscript
DisplayServer.screen_is_kept_on()
```

That previous state is restored when:

- the user stops the session;
- the session reaches its configured duration;
- the settings screen is opened while a session is active;
- the `Main` scene exits.

Pausing a session does not restore the previous screen sleep behavior, because the session is still active and the user may want to resume without the phone going to sleep.

## Settings screen

The settings screen is separate from the main screen.

It uses a fixed neutral style:

- black background;
- white text;
- white button borders;
- very dark button fill.

The settings screen does not preview the selected theme colors directly. This is intentional: some themes made buttons hard to read when the settings screen used theme colors.

The back button uses an SVG icon:

```text
assets/icons/back.svg
```

Current French layout example:

```text
[back] Réglages

Respiration

Inspiration        [−]  4.0s  [+]
Expiration         [−]  4.0s  [+]
Durée de séance     5 min
[slider]

Thèmes

[‹]  Océan  [›]
```

There is no save button. The old floppy-disk button was removed because immediate auto-save is simpler and more natural for this small mobile app.

### Settings editing model

The settings screen uses immediate application and immediate persistence:

- changing a value applies it to the active settings;
- changing a value writes it to `user://settings.cfg`;
- pressing the back button simply returns to the main screen;
- there is no cancel behavior anymore.

Affected values:

- inhalation duration;
- exhalation duration;
- total session duration;
- selected theme.

### Duration buttons

The duration buttons use:

```text
−
+
```

The values are edited in steps of:

```text
0.5 second
```

### Session duration slider

The total session duration is edited with a slider.

Default value:

```text
5 minutes
```

Limits:

```text
Minimum : 1 minute
Maximum : 60 minutes
```

Step:

```text
1 minute
```

The slider intentionally edits only whole minutes, not seconds.

### Theme buttons

The theme selection buttons use:

```text
‹
›
```

The selected theme name is displayed between the buttons.

The theme change is applied to the main screen and saved immediately.

## Breathing session and cycle

There are two timing layers:

1. the total session timer;
2. the repeated inhale/exhale breathing cycle.

### Session timer

The session timer is stored in seconds internally:

```text
_session_elapsed
```

The configured session duration comes from:

```gdscript
_settings.get_session_duration_seconds()
```

When:

```text
_session_elapsed >= session duration seconds
```

The app calls the completion flow, which fades to black, shows the completion message, resets the session, and returns to the start screen.

### Breathing cycle

The cycle has two phases:

```gdscript
BreathingPhase.INHALE
BreathingPhase.EXHALE
```

During inhalation:

```text
visual progress: 0 -> 1
ball movement  : bottom -> top
```

During exhalation:

```text
visual progress: 1 -> 0
ball movement  : top -> bottom
```

When the app starts:

- no session is running;
- the ball is visible at the bottom;
- the current phase is `INHALE`;
- elapsed phase time is `0`;
- elapsed session time is `0`.

## Eased movement

The ball does not move linearly.

Instead, the phase progress is passed through an easing function:

```gdscript
_ease_in_out(value)
```

The current easing uses a smootherstep formula:

```gdscript
return t * t * t * (t * (t * 6.0 - 15.0) + 10.0)
```

This gives a more natural breathing feeling:

- the ball starts slowly;
- it accelerates through the middle;
- it slows down near the top or bottom;
- there is no hard pause at either end.

This replaced the earlier idea of adding explicit pause durations after inhalation and exhalation, which felt too mechanical.

## `scripts/breathing_gauge.gd`

Custom `Control` that draws the gauge and the ball procedurally.

No image asset is used for the gauge or ball.

### Gauge shape

The gauge is a vertical capsule:

- one central rectangle;
- one circle at the top;
- one circle at the bottom.

The gauge has:

- rounded ends;
- no visible border;
- no side markers;
- no tick marks.

The gauge was reduced by roughly 10% compared with an earlier version, to feel lighter on a phone screen.

The main screen positions the gauge so it is visually centered in the screen.

### Ball

The ball is drawn with:

```gdscript
draw_circle(...)
```

It uses the full gauge width:

```text
ball radius = gauge width / 2
```

This means there is no side margin between the ball and the gauge.

The ball remains fully inside the capsule at the top and bottom.

Progress convention:

```text
0.0 : ball at the bottom
1.0 : ball at the top
```

The public method is:

```gdscript
set_progress(progress)
```

The progress value is clamped between `0.0` and `1.0`.

## `scripts/breathing_settings.gd`

Contains breathing parameters and color themes. Values are kept in memory during runtime and persisted by `settings_storage.gd` whenever the user changes a setting.

### Inhalation and exhalation durations

Default values:

```text
Inhalation : 4.0 seconds
Exhalation : 4.0 seconds
```

Step:

```text
0.5 second
```

Limits:

```text
Minimum : 1.0 second
Maximum : 20.0 seconds
```

The clamp method is:

```gdscript
clamp_duration(value)
```

### Session duration

Default value:

```text
5 minutes
```

Step:

```text
1 minute
```

Limits:

```text
Minimum : 1 minute
Maximum : 60 minutes
```

The active session duration is exposed as seconds through:

```gdscript
get_session_duration_seconds()
```

The clamp method is:

```gdscript
clamp_session_duration_minutes(value)
```

### Themes

Current theme keys:

```text
THEME_OCEAN
THEME_JUNGLE
THEME_VOLCANO
THEME_SKY
```

Localized names:

```text
English : Ocean, Jungle, Volcano, Sky
French  : Océan, Jungle, Volcan, Ciel
Spanish : Océano, Jungla, Volcán, Cielo
```

Each theme defines:

- background color;
- text color;
- gauge color;
- gauge border color;
- ball color.

Note: `gauge_border_color` still exists in the theme data, but the gauge no longer draws a visible border. It is kept for now to avoid unnecessary churn in the theme structure.

## `scripts/settings_storage.gd`

Static helper responsible for loading and saving settings in Godot's user data folder.

Storage path:

```text
user://settings.cfg
```

This is the portable Godot path for app-specific user data. On Android it resolves to the application's private writable storage, so no external storage permission is needed.

The file is a Godot `ConfigFile` with a `breathing` section.

Saved keys:

```text
inhale_duration
exhale_duration
session_duration_minutes
theme_index
```

Loading behavior:

- called during `Main._ready()` before building the UI;
- missing file means defaults are kept;
- invalid or missing values fall back to the current default values;
- loaded values are clamped through `breathing_settings.gd` before use.

Saving behavior:

- called after each settings change;
- writes the current active settings to `user://settings.cfg`;
- does not require Android external storage permissions.

## `scripts/app_localization.gd`

Small code-based localization helper.

The current app has only a few labels, so translations are currently stored in code rather than in a Godot CSV translation resource.

Responsibilities:

- detect the current locale from Godot;
- choose a supported language;
- provide fallback to English;
- translate UI labels and theme names;
- format the session duration label.

Supported languages:

```text
en : English
fr : French
es : Spanish
```

Current translated labels include:

```text
Settings
Breathing
Inhale
Exhale
Session duration
Themes
Session completed
Ocean
Jungle
Volcano
Sky
```

If the device language is not supported, the app falls back to English.

## Localization behavior

The language is selected automatically from the current OS/Godot locale.

On Android, this should follow the phone language or app language environment. The app does not currently offer an in-app language selector.

Potential future improvement:

- move translations from `app_localization.gd` to a Godot CSV translation resource if the number of labels grows.

## Removed legacy save icon files

The current UI no longer uses the old save icon system.

The following legacy files were removed on the `gdscript-port` branch:

```text
scripts/FloppyIcon.cs
scripts/FloppyIcon.cs.uid
assets/icons/floppy-disk.svg
assets/icons/floppy-disk.svg.import
```

They were used for the previous explicit save button, but the settings screen now auto-saves immediately after every change.

## Current validated state

Implemented and validated:

- standard Godot / non-.NET project opens and runs on desktop;
- Android APK export works with the standard Godot editor;
- APK size is much smaller than the previous C#/.NET version;
- main scene configured with `main.gd`;
- runtime UI built in GDScript;
- main breathing screen separated from settings screen;
- title removed from the main screen;
- settings button moved to the top-left;
- centered start/resume button;
- stop button appearing left of the resume button when paused;
- stop and start/resume buttons use SVG icons;
- settings back button uses a SVG icon;
- hidden controls during a running session;
- phone screen kept awake during an active breathing session;
- previous screen sleep behavior restored when the session stops, completes, or the scene exits;
- tap/click anywhere to pause while running;
- pause progress display above the gauge;
- progress bar based on total session duration;
- soft completion fade when session duration is reached;
- vertical rounded capsule-shaped gauge;
- gauge vertically centered on the main screen;
- gauge without visible border;
- gauge without side markers;
- gauge reduced by about 10%;
- ball using the full gauge width;
- eased breathing movement with slowdown near the top and bottom;
- editable inhalation/exhalation durations;
- editable total session duration with a whole-minute slider;
- theme switching;
- immediate settings application;
- immediate settings persistence in `user://settings.cfg`;
- basic localization in English, French, and Spanish;
- neutral black-and-white settings screen;
- GDScript scripts documented with comments.

## Technical points to watch

### Android export templates

If the standard Godot editor reports missing Android export templates, install the templates for the exact editor version:

```text
Editor > Manage Export Templates... > Download and Install
```

For example, Godot `4.6.2.stable` expects Android templates for `4.6.2.stable`, not the old `.mono` template folder.

### Screen wake behavior on Android

During a breathing session, the app uses Godot's screen keep-on flag so the phone should not enter automatic sleep while the gauge is moving.

For testing on a real Android phone:

1. set the phone screen timeout to a short value, for example 15 or 30 seconds;
2. start a breathing session;
3. wait longer than the normal screen timeout;
4. verify that the screen stays on;
5. stop the session;
6. verify that the phone can sleep normally again afterward.

The app does not prevent the user from manually turning the screen off with the power button.

### Settings persistence

Settings are stored in:

```text
user://settings.cfg
```

On Android, this is app-specific private storage. The app should keep the settings after closing and reopening it, as long as the app data is not cleared or the app is not uninstalled.

For testing:

1. change a setting;
2. verify that the main screen updates immediately;
3. close the app completely;
4. reopen it;
5. verify that the saved values are restored.

### Localization

Localization currently uses `app_localization.gd`.

For testing:

1. set the phone language to French;
2. open the app and verify French labels;
3. set the phone language to English;
4. reopen the app and verify English labels;
5. set the phone language to Spanish;
6. reopen the app and verify Spanish labels.

Watch for longer labels on small screens, especially in Spanish.

## Later improvements

Possible next steps:

- remove any temporary C# reference files after the GDScript migration is committed and validated;
- test settings persistence again on a real Android phone after reinstalling the APK;
- test screen wake behavior on a real Android phone;
- test localization on a real Android phone;
- prepare a clean release APK;
- optionally refine the completion fade timing after longer use;
- optionally add haptic feedback or sound, if it remains calm and unobtrusive.
