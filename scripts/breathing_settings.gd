extends RefCounted

## In-memory settings for the application.
##
## This object contains both timing values and the currently selected color theme.
## Persistence is handled by settings_storage.gd so this class stays focused on
## validation, defaults and theme application.

# Durations are edited in half-second steps, with hard limits to avoid values
# that would make the animation unusable.
const DURATION_STEP := 0.5
const MINIMUM_DURATION := 1.0
const MAXIMUM_DURATION := 20.0

# Whole-session duration is edited in minutes.
const SESSION_DURATION_STEP_MINUTES := 1
const MINIMUM_SESSION_DURATION_MINUTES := 1
const MAXIMUM_SESSION_DURATION_MINUTES := 60

# Each theme stores all colors needed by the breathing screen. The name is a
# localization key resolved by app_localization.gd.
const THEMES := [
	{
		"name_key": "THEME_OCEAN",
		"background_color": Color(0.00, 0.07, 0.18),
		"text_color": Color(0.86, 0.96, 1.00),
		"gauge_color": Color(0.00, 0.24, 0.48),
		"gauge_border_color": Color(0.00, 0.55, 0.95),
		"ball_color": Color(0.00, 0.78, 1.00),
	},
	{
		"name_key": "THEME_JUNGLE",
		# Inspired by the Color Hex palette 24608:
		# #63FF00, #00DD3B, #06B400, #008D02, #066916.
		"background_color": Color(0.02, 0.41, 0.09),
		"text_color": Color(0.95, 1.00, 0.92),
		"gauge_color": Color(0.00, 0.55, 0.01),
		"gauge_border_color": Color(0.00, 0.87, 0.23),
		"ball_color": Color(0.39, 1.00, 0.00),
	},
	{
		"name_key": "THEME_VOLCANO",
		# Inspired by the lava palette:
		# #370617, #6A040F, #9D0208, #D00000, #DC2F02, #E85D04, #F48C06, #FAA307, #FFBA08.
		"background_color": Color(0.22, 0.02, 0.09),
		"text_color": Color(1.00, 0.73, 0.03),
		"gauge_color": Color(0.62, 0.01, 0.03),
		"gauge_border_color": Color(0.96, 0.55, 0.02),
		"ball_color": Color(1.00, 0.73, 0.03),
	},
	{
		"name_key": "THEME_SKY",
		"background_color": Color(0.78, 0.92, 1.00),
		"text_color": Color(0.04, 0.16, 0.32),
		"gauge_color": Color(0.96, 0.99, 1.00),
		"gauge_border_color": Color(0.42, 0.76, 1.00),
		"ball_color": Color(0.18, 0.62, 1.00),
	},
]

var inhale_duration := 4.0
var exhale_duration := 4.0
var session_duration_minutes := 5
var current_theme_index := 0

var background_color := Color(0.00, 0.07, 0.18)
var text_color := Color(0.86, 0.96, 1.00)
var gauge_color := Color(0.00, 0.24, 0.48)
var gauge_border_color := Color(0.00, 0.55, 0.95)
var ball_color := Color(0.00, 0.78, 1.00)


func _init() -> void:
	apply_theme(0)


func get_session_duration_seconds() -> float:
	return float(session_duration_minutes) * 60.0


func get_current_theme_name_key() -> String:
	return THEMES[current_theme_index]["name_key"]


func move_to_next_theme() -> void:
	apply_theme(current_theme_index + 1)


func move_to_previous_theme() -> void:
	apply_theme(current_theme_index - 1)


## Wraps the theme index, then copies the selected palette into direct fields so
## the UI can read colors without knowing the internal theme dictionary format.
func apply_theme(theme_index: int) -> void:
	current_theme_index = wrap_index(theme_index, THEMES.size())
	var theme: Dictionary = THEMES[current_theme_index]

	background_color = theme["background_color"]
	text_color = theme["text_color"]
	gauge_color = theme["gauge_color"]
	gauge_border_color = theme["gauge_border_color"]
	ball_color = theme["ball_color"]


static func clamp_duration(value: float) -> float:
	return clampf(value, MINIMUM_DURATION, MAXIMUM_DURATION)


static func clamp_session_duration_minutes(value: int) -> int:
	return clampi(value, MINIMUM_SESSION_DURATION_MINUTES, MAXIMUM_SESSION_DURATION_MINUTES)


## Modulo helper that also handles negative values, useful for previous/next
## theme navigation.
static func wrap_index(value: int, length: int) -> int:
	if length <= 0:
		return 0

	var result := value % length
	if result < 0:
		result += length
	return result
