# Android export notes

This document summarizes the Android-specific setup used by SimpleBreathing.

SimpleBreathing is built with **Godot 4.6.2** and **GDScript**. The active project is intended to export with the standard **non-.NET** Godot editor.

## Current Android status

The GDScript version has been tested through an Android APK export using the standard Godot editor. The APK size is much smaller than the previous C#/.NET export.

The project should no longer require:

- Godot .NET;
- `.csproj` / `.sln` files;
- the .NET SDK;
- C# Android export templates;
- MSBuild.

It still requires the normal Android export toolchain.

## Required local tools

For Android export, configure the usual Godot Android export dependencies:

- Android SDK;
- Android SDK Platform-Tools;
- Android SDK Build-Tools;
- Android SDK Command-line Tools;
- Android NDK;
- CMake;
- OpenJDK.

Also install the Android export templates for the exact Godot editor version being used. For example, Godot `4.6.2.stable` expects templates under a matching `4.6.2.stable` export-template folder.

If Godot reports errors such as:

```text
No export template found at the expected path:
.../export_templates/4.6.2.stable/android_debug.apk
.../export_templates/4.6.2.stable/android_release.apk
```

install the templates from:

```text
Editor > Manage Export Templates... > Download and Install
```

## Recommended Android screen settings

The app should behave like a small utility app, not like a fullscreen game. The Android navigation bar should remain visible so the user can leave the app normally.

Recommended Android export preset settings:

```text
Project > Export > Android > Options > Screen

Immersive Mode: Off
Edge to Edge: On
```

Expected behavior:

- the Android navigation bar remains visible;
- the app background can extend behind translucent system bars;
- interactive UI controls stay inside the safe area;
- the main screen and settings screen avoid being hidden under system bars.

`export_presets.cfg` may be local-only depending on the repository setup, so these settings may need to be checked manually after cloning the project on another machine.

## Runtime system-bar handling

`scripts/main.gd` also includes a runtime safety net for Android:

```gdscript
DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
```

This keeps the app out of immersive fullscreen mode if a local export preset accidentally enables it.

The UI margins are adjusted using:

```gdscript
DisplayServer.get_display_safe_area()
```

The full-screen background still covers the whole window, including translucent system-bar areas, but controls remain inside safe-area margins.

## Screen wake behavior

During an active breathing session, the app keeps the device screen awake with:

```gdscript
DisplayServer.screen_set_keep_on(true)
```

The previous keep-screen-on state is saved before enabling this flag and restored when the session stops, completes, or the scene exits.

Pausing a session does not restore the previous sleep behavior because the session is still active and may be resumed.

## Boot splash and launcher icon

The Godot boot splash image is disabled in `project.godot`:

```text
boot_splash/show_image=false
```

This removes Godot's default splash image. Android may still briefly show the app launcher icon during startup. To avoid seeing the Godot icon there, configure custom Android launcher icons in the Android export preset.

## Files affected by Android behavior

Android-related runtime behavior is mainly handled by:

```text
project.godot
scripts/main.gd
docs/current_implementation.md
docs/android_export_notes.md
README.md
```

Generated or local files should normally not be committed:

```text
.godot/
export_presets.cfg   # depending on whether local export presets are intentionally tracked
```

## Test checklist

After changing Android behavior, test these points on a real phone:

1. export and install the APK;
2. verify that the app opens with the expected portrait layout;
3. verify that the Android navigation bar remains visible;
4. verify that buttons and sliders are not under system bars;
5. start a session and confirm that the screen stays awake;
6. pause and resume the session;
7. stop the session and confirm that the phone can sleep normally again;
8. change settings, close the app, reopen it, and confirm that settings persist;
9. check that SVG icons render correctly on Android.
