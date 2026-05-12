extends RefCounted

const BreathingSettings := preload("res://scripts/breathing_settings.gd")

## Loads and saves breathing settings in Godot's user data folder.
##
## The path uses user://, which is the portable Godot way to store app data. On
## Android this resolves to the app's private writable storage, so it works
## without requesting external storage permissions.

const SETTINGS_PATH := "user://settings.cfg"
const SETTINGS_SECTION := "breathing"

const INHALE_DURATION_KEY := "inhale_duration"
const EXHALE_DURATION_KEY := "exhale_duration"
const SESSION_DURATION_MINUTES_KEY := "session_duration_minutes"
const THEME_INDEX_KEY := "theme_index"


## Loads persisted settings into the provided BreathingSettings instance.
## Invalid or missing values fall back to the current defaults and are clamped.
static func load_settings(settings) -> bool:
	if not FileAccess.file_exists(SETTINGS_PATH):
		return false

	var config := ConfigFile.new()
	var error := config.load(SETTINGS_PATH)

	if error != OK:
		push_warning("Could not load settings from %s: %s" % [SETTINGS_PATH, error])
		return false

	settings.inhale_duration = BreathingSettings.clamp_duration(
		_read_float(config, INHALE_DURATION_KEY, settings.inhale_duration)
	)
	settings.exhale_duration = BreathingSettings.clamp_duration(
		_read_float(config, EXHALE_DURATION_KEY, settings.exhale_duration)
	)
	settings.session_duration_minutes = BreathingSettings.clamp_session_duration_minutes(
		_read_int(config, SESSION_DURATION_MINUTES_KEY, settings.session_duration_minutes)
	)
	settings.apply_theme(_read_int(config, THEME_INDEX_KEY, settings.current_theme_index))

	return true


## Saves the currently active settings. Called immediately after each user change.
static func save_settings(settings) -> bool:
	var config := ConfigFile.new()

	config.set_value(SETTINGS_SECTION, INHALE_DURATION_KEY, settings.inhale_duration)
	config.set_value(SETTINGS_SECTION, EXHALE_DURATION_KEY, settings.exhale_duration)
	config.set_value(SETTINGS_SECTION, SESSION_DURATION_MINUTES_KEY, settings.session_duration_minutes)
	config.set_value(SETTINGS_SECTION, THEME_INDEX_KEY, settings.current_theme_index)

	var error := config.save(SETTINGS_PATH)

	if error != OK:
		push_warning("Could not save settings to %s: %s" % [SETTINGS_PATH, error])
		return false

	return true


## Typed accessors keep load_settings() readable and preserve defaults for
## settings that do not exist yet, for example after an app update.
static func _read_float(config: ConfigFile, key: String, default_value: float) -> float:
	if not config.has_section_key(SETTINGS_SECTION, key):
		return default_value

	return float(config.get_value(SETTINGS_SECTION, key, default_value))


static func _read_int(config: ConfigFile, key: String, default_value: int) -> int:
	if not config.has_section_key(SETTINGS_SECTION, key):
		return default_value

	return int(config.get_value(SETTINGS_SECTION, key, default_value))
