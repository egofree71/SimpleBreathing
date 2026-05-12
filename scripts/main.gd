extends Control

## Main application controller.
##
## The scene file intentionally stays almost empty: this script builds the UI in
## code so the C# and GDScript versions can be compared more easily during the
## migration. The application has three visual layers:
## - the main breathing screen;
## - the settings screen;
## - a temporary completion overlay shown at the end of a session.
##
## Session state is controlled by two booleans:
## - _has_session_started: the user has pressed Start at least once;
## - _is_running: the breathing animation is currently advancing.
## This gives three simple states: waiting, running, paused.

const AppLocalization := preload("res://scripts/app_localization.gd")
const BreathingSettings := preload("res://scripts/breathing_settings.gd")
const SettingsStorage := preload("res://scripts/settings_storage.gd")
const BreathingGauge := preload("res://scripts/breathing_gauge.gd")

# SVG icons are used instead of text glyphs. This avoids small rendering
# differences between desktop fonts and Android fonts, especially for the stop
# square and the back arrow.
const BACK_ICON := preload("res://assets/icons/back.svg")
const STOP_ICON := preload("res://assets/icons/stop.svg")
const PLAY_ICON := preload("res://assets/icons/play.svg")

enum BreathingPhase { INHALE, EXHALE }

# Fixed visual size used by the main start/resume/stop buttons.
#
# These buttons are positioned manually so the play/resume button stays centered
# and the stop button appears to its left only while the session is paused.
const SESSION_BUTTON_SIZE := Vector2(80, 68)
const SESSION_BUTTON_CENTER_Y := 16.0
const STOP_BUTTON_CENTER_X := -92.0
const START_RESUME_BUTTON_CENTER_X := 0.0

# Active breathing durations and color theme selection.
var _settings := BreathingSettings.new()

# Working values shown on the settings screen. They are applied and saved
# immediately after each user change.
var _draft_inhale_duration := 4.0
var _draft_exhale_duration := 4.0
var _draft_session_duration_minutes := 5
var _draft_theme_index := 0

# Avoids reacting to slider changes caused by update_texts() itself.
var _is_updating_settings_controls := false

# Custom Control responsible for drawing the gauge and moving ball.
var _gauge

# Settings-screen labels refreshed whenever durations, session length, or theme change.
var _inhale_value_label: Label
var _exhale_value_label: Label
var _session_duration_value_label: Label
var _session_duration_slider: HSlider
var _theme_label: Label

# Main-screen controls. The containers stay visible to keep the gauge size stable.
var _settings_button: Button
var _start_resume_button: Button
var _stop_button: Button
var _top_action_row: Control
var _bottom_controls: Control

# Margins that hold interactive content inside Android/iOS safe areas while the
# full-screen background can still extend behind translucent system bars.
var _main_screen_margin: MarginContainer
var _settings_screen_margin: MarginContainer

# Pause-only information shown above the gauge.
var _pause_elapsed_label: Label
var _pause_progress_bar: Control
var _pause_progress_fill: ColorRect

# Transparent full-screen touch area used only while the breathing session is running.
var _pause_touch_area: Control

# Full-screen background color rectangle used instead of relying on theme defaults.
var _background: ColorRect

# Two top-level screen containers. Only one is visible at a time.
var _main_screen: Control
var _settings_screen: Control

# Full-screen overlay used when a session finishes naturally.
var _completion_overlay: Control
var _completion_fade: ColorRect
var _completion_label: Label

# Prevents multiple completion animations from being started by the same session.
var _is_showing_completion := false

# Current breathing phase, elapsed time inside that phase, and total elapsed session time.
var _current_phase := BreathingPhase.INHALE
var _phase_elapsed := 0.0
var _session_elapsed := 0.0

# A session starts only after the user presses the start button.
var _has_session_started := false

# False on startup: the ball is visible at the bottom, but the cycle is not moving yet.
var _is_running := false

# Remembers whether the device was already configured to keep the screen on
# before the breathing session started, so the app can restore that state later.
var _was_screen_kept_on_before_session := false

# True only while this controller has explicitly enabled the keep-screen-on flag.
var _is_keeping_screen_on_for_session := false


## Entry point called when the scene is loaded.
## Loads saved settings, builds the interface, then synchronizes colors, texts
## and gauge position with the current state.
func _ready() -> void:
	# Android export presets can enable immersive fullscreen mode, which hides the
	# system navigation bar. SimpleBreathing is closer to a small utility app than
	# to a game, so keep Android system bars visible by default.
	_ensure_android_system_bars_visible()

	# Load persisted values before building the UI so labels, sliders, and colors
	# start with the previously saved settings on every platform.
	SettingsStorage.load_settings(_settings)

	_build_interface()
	resized.connect(_update_safe_area_margins)
	_update_safe_area_margins()
	_reset_draft_settings_from_current()
	_apply_colors()
	_update_texts()
	_update_gauge()
	_update_pause_progress_display()
	_update_main_screen_visibility()


## Keep Android system bars visible. The app is a calm utility, not a fullscreen
## game, so hiding navigation controls would feel unnecessarily aggressive.
static func _ensure_android_system_bars_visible() -> void:
	if OS.get_name() != "Android":
		return

	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)


## Advances the breathing cycle while a session is running.
## The gauge is updated from elapsed time, not from a tween, so pause/resume and
## mobile app lifecycle interruptions remain predictable.
func _process(delta: float) -> void:
	if not _is_running:
		return

	_session_elapsed += delta

	if _session_elapsed >= _get_session_duration():
		_complete_breathing_session()
		return

	_phase_elapsed += delta

	# Use a while loop instead of a single if so the cycle remains valid even after
	# a large frame hitch, for example when the app is resumed on mobile.
	while _phase_elapsed >= _get_current_phase_duration():
		_phase_elapsed -= _get_current_phase_duration()
		_current_phase = BreathingPhase.EXHALE if _current_phase == BreathingPhase.INHALE else BreathingPhase.INHALE

	_update_gauge()
	_update_pause_progress_display()


## Safety hook: if the app exits during a session, restore the previous
## keep-screen-on setting.
func _exit_tree() -> void:
	_restore_screen_keep_on_state()


## Builds the three top-level UI layers.
## Keeping them as separate controls makes screen switching simple: only one main
## screen is visible at a time, while the completion overlay sits above both.
func _build_interface() -> void:
	_background = ColorRect.new()
	_background.name = "Background"
	_background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_background)
	_fill_parent(_background)

	_main_screen = _build_main_screen()
	add_child(_main_screen)
	_fill_parent(_main_screen)

	_settings_screen = _build_settings_screen()
	_settings_screen.visible = false
	add_child(_settings_screen)
	_fill_parent(_settings_screen)

	_completion_overlay = _build_completion_overlay()
	add_child(_completion_overlay)
	_fill_parent(_completion_overlay)


## Applies platform safe-area margins to interactive content.
## This keeps buttons away from notches, rounded corners and Android system bars,
## while the background still fills the whole screen.
func _update_safe_area_margins() -> void:
	if _main_screen_margin == null or _settings_screen_margin == null:
		return

	var safe_margins := _get_safe_area_margins_in_viewport_units()

	_apply_margin_overrides(
		_main_screen_margin,
		24.0 + safe_margins.x,
		16.0 + safe_margins.y,
		24.0 + safe_margins.z,
		24.0 + safe_margins.w
	)

	_apply_margin_overrides(
		_settings_screen_margin,
		24.0 + safe_margins.x,
		24.0 + safe_margins.y,
		24.0 + safe_margins.z,
		24.0 + safe_margins.w
	)

	if _top_action_row != null:
		_top_action_row.position = Vector2(20.0 + safe_margins.x, 16.0 + safe_margins.y)


## Converts the display safe area from physical window pixels into the current
## viewport coordinate system used by Control nodes. Returns left/top/right/bottom.
func _get_safe_area_margins_in_viewport_units() -> Vector4:
	var os_name := OS.get_name()
	if os_name != "Android" and os_name != "iOS":
		return Vector4.ZERO

	var viewport_size := get_viewport_rect().size
	var window_size := DisplayServer.window_get_size()
	if viewport_size.x <= 0.0 or viewport_size.y <= 0.0 or window_size.x <= 0 or window_size.y <= 0:
		return Vector4.ZERO

	var safe_area := DisplayServer.get_display_safe_area()
	if safe_area.size.x <= 0 or safe_area.size.y <= 0:
		return Vector4.ZERO

	var left_pixels: int = maxi(0, safe_area.position.x)
	var top_pixels: int = maxi(0, safe_area.position.y)
	var right_pixels: int = maxi(0, window_size.x - (safe_area.position.x + safe_area.size.x))
	var bottom_pixels: int = maxi(0, window_size.y - (safe_area.position.y + safe_area.size.y))

	var scale_x := viewport_size.x / float(window_size.x)
	var scale_y := viewport_size.y / float(window_size.y)

	return Vector4(
		left_pixels * scale_x,
		top_pixels * scale_y,
		right_pixels * scale_x,
		bottom_pixels * scale_y
	)


static func _apply_margin_overrides(margin: MarginContainer, left: float, top: float, right: float, bottom: float) -> void:
	margin.add_theme_constant_override("margin_left", _to_margin_constant(left))
	margin.add_theme_constant_override("margin_top", _to_margin_constant(top))
	margin.add_theme_constant_override("margin_right", _to_margin_constant(right))
	margin.add_theme_constant_override("margin_bottom", _to_margin_constant(bottom))


static func _to_margin_constant(value: float) -> int:
	return maxi(0, int(roundf(value)))


## Creates the black fade overlay shown when a breathing session finishes
## naturally. The overlay blocks input during the short end animation.
func _build_completion_overlay() -> Control:
	var overlay := Control.new()
	overlay.name = "CompletionOverlay"
	overlay.visible = false
	overlay.mouse_filter = Control.MOUSE_FILTER_STOP

	_completion_fade = ColorRect.new()
	_completion_fade.name = "CompletionFade"
	_completion_fade.color = Color.BLACK
	_completion_fade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	overlay.add_child(_completion_fade)
	_fill_parent(_completion_fade)

	var center := CenterContainer.new()
	center.name = "CompletionMessageCenter"
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	overlay.add_child(center)
	_fill_parent(center)

	_completion_label = Label.new()
	_completion_label.name = "CompletionLabel"
	_completion_label.text = AppLocalization.translate(AppLocalization.SESSION_COMPLETED)
	_completion_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_completion_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_completion_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_completion_label.add_theme_font_size_override("font_size", 34)
	_completion_label.add_theme_color_override("font_color", Color.WHITE)
	center.add_child(_completion_label)

	_completion_fade.modulate = Color(1.0, 1.0, 1.0, 0.0)
	_completion_label.modulate = Color(1.0, 1.0, 1.0, 0.0)

	return overlay


## Builds the breathing screen: optional pause progress at the top, the gauge in
## the middle, and the start/resume/stop controls at the bottom.
func _build_main_screen() -> Control:
	var root := Control.new()
	root.name = "MainScreen"

	_main_screen_margin = MarginContainer.new()
	_main_screen_margin.name = "MainScreenMargin"
	root.add_child(_main_screen_margin)
	_fill_parent(_main_screen_margin)

	var main_column := VBoxContainer.new()
	main_column.name = "MainColumn"
	main_column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	main_column.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_column.add_theme_constant_override("separation", 8)
	_main_screen_margin.add_child(main_column)

	# Keep the settings button as an overlay instead of placing it in the vertical
	# layout. This prevents it from pushing the gauge downward.
	_top_action_row = _build_top_action_row()
	root.add_child(_top_action_row)

	# The pause progress area and the bottom controls have the same reserved height,
	# so the gauge is vertically centered in the screen.
	main_column.add_child(_build_pause_progress_area())

	_gauge = BreathingGauge.new()
	_gauge.name = "BreathingGauge"
	_gauge.custom_minimum_size = Vector2(260, 520)
	_gauge.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_gauge.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_column.add_child(_gauge)

	_bottom_controls = _build_bottom_controls()
	main_column.add_child(_bottom_controls)

	_pause_touch_area = Control.new()
	_pause_touch_area.name = "PauseTouchArea"
	_pause_touch_area.mouse_filter = Control.MOUSE_FILTER_STOP
	_pause_touch_area.visible = false
	_pause_touch_area.gui_input.connect(_on_pause_touch_area_gui_input)
	root.add_child(_pause_touch_area)
	_fill_parent(_pause_touch_area)

	return root


## Builds the settings button row. It is overlaid on top of the main layout so it
## does not affect gauge centering.
func _build_top_action_row() -> Control:
	var row := HBoxContainer.new()
	row.name = "TopActionRow"
	row.position = Vector2(20, 16)
	row.size = Vector2(64, 56)
	row.custom_minimum_size = Vector2(64, 56)
	row.add_theme_constant_override("separation", 12)

	_settings_button = _create_icon_button("⚙", Callable(self, "_show_settings_screen"), 34)
	row.add_child(_settings_button)

	var spacer := Control.new()
	spacer.name = "TopActionSpacer"
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(spacer)

	return row


## Builds the progress display shown only while the session is paused.
## The area keeps a reserved height even when hidden so the gauge does not jump.
func _build_pause_progress_area() -> Control:
	var area := Control.new()
	area.name = "PauseProgressArea"
	area.custom_minimum_size = Vector2(0, 64)
	area.size_flags_horizontal = Control.SIZE_EXPAND_FILL

	var column := VBoxContainer.new()
	column.name = "PauseProgressColumn"
	column.alignment = BoxContainer.ALIGNMENT_CENTER
	column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	column.size_flags_vertical = Control.SIZE_EXPAND_FILL
	column.add_theme_constant_override("separation", 8)
	area.add_child(column)
	_fill_parent(column)
	# Move the pause progress display slightly closer to the gauge.
	column.offset_top = 14.0

	_pause_elapsed_label = Label.new()
	_pause_elapsed_label.name = "PauseElapsedLabel"
	_pause_elapsed_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_pause_elapsed_label.visible = false
	_pause_elapsed_label.add_theme_font_size_override("font_size", 24)
	column.add_child(_pause_elapsed_label)

	var bar_center := CenterContainer.new()
	bar_center.name = "PauseProgressBarCenter"
	bar_center.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bar_center.visible = false
	column.add_child(bar_center)

	_pause_progress_bar = Control.new()
	_pause_progress_bar.name = "PauseProgressBar"
	# 360px is roughly three quarters of the current 480px base viewport width.
	_pause_progress_bar.custom_minimum_size = Vector2(360, 16)
	_pause_progress_bar.clip_contents = true
	_pause_progress_bar.resized.connect(_update_pause_progress_display)
	bar_center.add_child(_pause_progress_bar)

	var background := ColorRect.new()
	background.name = "PauseProgressBarBackground"
	background.color = Color(1.0, 1.0, 1.0, 0.25)
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_pause_progress_bar.add_child(background)
	_fill_parent(background)

	_pause_progress_fill = ColorRect.new()
	_pause_progress_fill.name = "PauseProgressBarFill"
	_pause_progress_fill.color = Color(1.0, 1.0, 1.0, 0.95)
	_pause_progress_fill.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_pause_progress_bar.add_child(_pause_progress_fill)
	_pause_progress_fill.anchor_left = 0.0
	_pause_progress_fill.anchor_top = 0.0
	_pause_progress_fill.anchor_right = 0.0
	_pause_progress_fill.anchor_bottom = 1.0
	_pause_progress_fill.offset_left = 0.0
	_pause_progress_fill.offset_top = 0.0
	_pause_progress_fill.offset_right = 0.0
	_pause_progress_fill.offset_bottom = 0.0

	return area


## Builds the bottom control area. The play/resume button remains centered; the
## stop button appears on the left only after the session has been paused.
func _build_bottom_controls() -> Control:
	var controls := Control.new()
	controls.name = "BottomControls"
	controls.custom_minimum_size = Vector2(0, 64)
	controls.size_flags_horizontal = Control.SIZE_EXPAND_FILL

	_stop_button = _create_session_svg_icon_button(STOP_ICON, Callable(self, "_stop_breathing_session"))
	controls.add_child(_stop_button)

	_start_resume_button = _create_session_svg_icon_button(PLAY_ICON, Callable(self, "_start_or_resume_breathing_session"))
	controls.add_child(_start_resume_button)

	# The resume button is anchored to the exact horizontal center. When the stop
	# button appears on the left during pause, the resume button does not shift.
	_position_session_button(_start_resume_button, START_RESUME_BUTTON_CENTER_X)
	_position_session_button(_stop_button, STOP_BUTTON_CENTER_X)

	return controls


## Builds the settings screen. Changes are applied immediately, so there is no
## separate Save button and no pending state to confirm.
func _build_settings_screen() -> Control:
	var root := Control.new()
	root.name = "SettingsScreenRoot"

	# Keep the settings screen visually neutral: solid black background and white
	# controls, regardless of the currently selected breathing theme.
	var background := ColorRect.new()
	background.name = "SettingsBackground"
	background.color = Color.BLACK
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(background)
	_fill_parent(background)

	_settings_screen_margin = MarginContainer.new()
	_settings_screen_margin.name = "SettingsScreen"
	root.add_child(_settings_screen_margin)
	_fill_parent(_settings_screen_margin)

	var column := VBoxContainer.new()
	column.name = "SettingsColumn"
	column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	column.size_flags_vertical = Control.SIZE_EXPAND_FILL
	column.add_theme_constant_override("separation", 18)
	_settings_screen_margin.add_child(column)

	var header_row := HBoxContainer.new()
	header_row.name = "SettingsHeader"
	header_row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header_row.add_theme_constant_override("separation", 12)
	column.add_child(header_row)

	header_row.add_child(_create_settings_svg_icon_button(BACK_ICON, Callable(self, "_show_main_screen")))

	var title := Label.new()
	title.text = AppLocalization.translate(AppLocalization.SETTINGS_TITLE)
	title.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title.add_theme_font_size_override("font_size", 28)
	header_row.add_child(title)

	column.add_child(_create_vertical_spacer(10))

	var breathing_title := _create_section_title(AppLocalization.translate(AppLocalization.BREATHING_SECTION))
	column.add_child(breathing_title)

	column.add_child(_create_duration_row(
		AppLocalization.translate(AppLocalization.INHALE),
		"_inhale_value_label",
		Callable(self, "_decrease_inhale_duration"),
		Callable(self, "_increase_inhale_duration")
	))

	column.add_child(_create_duration_row(
		AppLocalization.translate(AppLocalization.EXHALE),
		"_exhale_value_label",
		Callable(self, "_decrease_exhale_duration"),
		Callable(self, "_increase_exhale_duration")
	))

	column.add_child(_create_session_duration_slider())

	column.add_child(_create_vertical_spacer(10))

	var colors_title := _create_section_title(AppLocalization.translate(AppLocalization.THEMES_SECTION))
	column.add_child(colors_title)
	column.add_child(_create_theme_row())

	# Keeps the settings content near the top without adding a separate Save button.
	# Each setting is applied and persisted immediately when the user changes it.
	var flexible_spacer := Control.new()
	flexible_spacer.name = "SettingsFlexibleSpacer"
	flexible_spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	column.add_child(flexible_spacer)

	return root


func _create_section_title(text: String) -> Label:
	var label := Label.new()
	label.text = text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	label.add_theme_font_size_override("font_size", 22)
	return label


## Creates one +/- duration row for inhale or exhale.
## The value label is stored in a member variable so _update_texts() can refresh
## it after each change.
func _create_duration_row(label_text: String, value_label_member_name: String, decrease: Callable, increase: Callable) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.name = label_text + "Row"
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("separation", 10)

	var label := Label.new()
	label.text = label_text
	label.custom_minimum_size = Vector2(120, 0)
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", 20)
	row.add_child(label)

	row.add_child(_create_settings_button("−", decrease))

	var value_label := Label.new()
	value_label.custom_minimum_size = Vector2(72, 0)
	value_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	value_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	value_label.add_theme_font_size_override("font_size", 20)
	row.add_child(value_label)

	if value_label_member_name == "_inhale_value_label":
		_inhale_value_label = value_label
	elif value_label_member_name == "_exhale_value_label":
		_exhale_value_label = value_label

	row.add_child(_create_settings_button("+", increase))

	return row


## Creates the session-duration slider. Values are rounded to whole minutes and
## clamped by BreathingSettings.
func _create_session_duration_slider() -> VBoxContainer:
	var container := VBoxContainer.new()
	container.name = "SessionDurationContainer"
	container.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	container.add_theme_constant_override("separation", 8)

	var row := HBoxContainer.new()
	row.name = "SessionDurationRow"
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("separation", 10)
	container.add_child(row)

	var label := Label.new()
	label.text = AppLocalization.translate(AppLocalization.SESSION_DURATION)
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", 20)
	row.add_child(label)

	_session_duration_value_label = Label.new()
	_session_duration_value_label.name = "SessionDurationValueLabel"
	_session_duration_value_label.custom_minimum_size = Vector2(90, 0)
	_session_duration_value_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_session_duration_value_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_session_duration_value_label.add_theme_font_size_override("font_size", 20)
	row.add_child(_session_duration_value_label)

	_session_duration_slider = HSlider.new()
	_session_duration_slider.name = "SessionDurationSlider"
	_session_duration_slider.min_value = BreathingSettings.MINIMUM_SESSION_DURATION_MINUTES
	_session_duration_slider.max_value = BreathingSettings.MAXIMUM_SESSION_DURATION_MINUTES
	_session_duration_slider.step = BreathingSettings.SESSION_DURATION_STEP_MINUTES
	_session_duration_slider.value = _settings.session_duration_minutes
	_session_duration_slider.custom_minimum_size = Vector2(0, 42)
	_session_duration_slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_session_duration_slider.value_changed.connect(_on_session_duration_slider_value_changed)
	container.add_child(_session_duration_slider)

	return container


## Creates the theme picker. Theme names are localization keys, not hard-coded
## labels, so they follow the device language.
func _create_theme_row() -> HBoxContainer:
	var row := HBoxContainer.new()
	row.name = "ThemeRow"
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("separation", 10)

	row.add_child(_create_settings_button("‹", Callable(self, "_select_previous_theme")))

	_theme_label = Label.new()
	_theme_label.name = "ThemeLabel"
	_theme_label.custom_minimum_size = Vector2(180, 0)
	_theme_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_theme_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_theme_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_theme_label.add_theme_font_size_override("font_size", 20)
	row.add_child(_theme_label)

	row.add_child(_create_settings_button("›", Callable(self, "_select_next_theme")))

	return row


func _create_button(text: String, on_pressed: Callable) -> Button:
	var button := Button.new()
	button.text = text
	button.custom_minimum_size = Vector2(112, 48)
	button.pressed.connect(on_pressed)
	return button


func _create_icon_button(text: String, on_pressed: Callable, font_size := 28) -> Button:
	var button := Button.new()
	button.text = text
	button.custom_minimum_size = Vector2(64, 56)
	button.add_theme_font_size_override("font_size", font_size)
	button.pressed.connect(on_pressed)
	return button


## Creates a large SVG-icon button for the main breathing controls.
## expand_icon makes the SVG fill the button consistently on desktop and Android.
func _create_session_svg_icon_button(icon_texture: Texture2D, on_pressed: Callable) -> Button:
	var button := Button.new()
	button.text = ""
	button.icon = icon_texture
	button.expand_icon = true
	button.icon_alignment = HORIZONTAL_ALIGNMENT_CENTER
	button.custom_minimum_size = SESSION_BUTTON_SIZE
	button.size = SESSION_BUTTON_SIZE
	button.pressed.connect(on_pressed)
	return button


func _create_settings_button(text: String, on_pressed: Callable) -> Button:
	var button := Button.new()
	button.text = text
	button.custom_minimum_size = Vector2(112, 48)
	button.add_theme_font_size_override("font_size", 30)
	_apply_settings_button_style(button)
	button.pressed.connect(on_pressed)
	return button


## Creates a small SVG-icon button for the settings header.
func _create_settings_svg_icon_button(icon_texture: Texture2D, on_pressed: Callable) -> Button:
	var button := Button.new()
	button.text = ""
	button.icon = icon_texture
	button.expand_icon = true
	button.icon_alignment = HORIZONTAL_ALIGNMENT_CENTER
	button.custom_minimum_size = Vector2(64, 56)
	_apply_settings_button_style(button)
	button.pressed.connect(on_pressed)
	return button


## Styles the translucent buttons on the main screen.
## The optional content shift exists for tiny visual corrections without changing
## button geometry.
static func _apply_main_button_style(button: Button, icon_color: Color, content_shift_x := 0.0, content_shift_y := 0.0) -> void:
	button.add_theme_color_override("font_color", icon_color)
	button.add_theme_color_override("font_hover_color", icon_color)
	button.add_theme_color_override("font_pressed_color", icon_color)
	button.add_theme_color_override("font_focus_color", icon_color)
	button.add_theme_color_override("font_disabled_color", Color(icon_color.r, icon_color.g, icon_color.b, 0.45))

	button.add_theme_stylebox_override("normal", _create_main_button_style_box(icon_color, 0.10, 0.16, content_shift_x, content_shift_y))
	button.add_theme_stylebox_override("hover", _create_main_button_style_box(icon_color, 0.16, 0.22, content_shift_x, content_shift_y))
	button.add_theme_stylebox_override("pressed", _create_main_button_style_box(icon_color, 0.24, 0.30, content_shift_x, content_shift_y))
	button.add_theme_stylebox_override("focus", _create_main_button_style_box(icon_color, 0.10, 0.32, content_shift_x, content_shift_y))
	button.add_theme_stylebox_override("disabled", _create_main_button_style_box(icon_color, 0.06, 0.10, content_shift_x, content_shift_y))


static func _create_main_button_style_box(color: Color, background_alpha: float, border_alpha: float, content_shift_x := 0.0, content_shift_y := 0.0) -> StyleBoxFlat:
	const BASE_CONTENT_MARGIN := 8.0

	var style := StyleBoxFlat.new()
	style.bg_color = Color(color.r, color.g, color.b, background_alpha)
	style.border_color = Color(color.r, color.g, color.b, border_alpha)
	style.border_width_left = 1
	style.border_width_top = 1
	style.border_width_right = 1
	style.border_width_bottom = 1
	style.corner_radius_top_left = 8
	style.corner_radius_top_right = 8
	style.corner_radius_bottom_right = 8
	style.corner_radius_bottom_left = 8
	style.content_margin_left = maxf(0.0, BASE_CONTENT_MARGIN + content_shift_x)
	style.content_margin_top = maxf(0.0, BASE_CONTENT_MARGIN + content_shift_y)
	style.content_margin_right = maxf(0.0, BASE_CONTENT_MARGIN - content_shift_x)
	style.content_margin_bottom = maxf(0.0, BASE_CONTENT_MARGIN - content_shift_y)

	return style


## Styles settings-screen buttons in neutral black and white, independent from
## the selected breathing theme.
static func _apply_settings_button_style(button: Button) -> void:
	button.add_theme_color_override("font_color", Color.WHITE)
	button.add_theme_color_override("font_hover_color", Color.WHITE)
	button.add_theme_color_override("font_pressed_color", Color.WHITE)
	button.add_theme_color_override("font_focus_color", Color.WHITE)
	button.add_theme_color_override("font_outline_color", Color.WHITE)
	button.add_theme_constant_override("outline_size", 1)

	button.add_theme_stylebox_override("normal", _create_settings_button_style_box(Color(0.04, 0.04, 0.04)))
	button.add_theme_stylebox_override("hover", _create_settings_button_style_box(Color(0.10, 0.10, 0.10)))
	button.add_theme_stylebox_override("pressed", _create_settings_button_style_box(Color(0.18, 0.18, 0.18)))
	button.add_theme_stylebox_override("focus", _create_settings_button_style_box(Color(0.04, 0.04, 0.04)))
	button.add_theme_stylebox_override("disabled", _create_settings_button_style_box(Color(0.04, 0.04, 0.04)))


static func _create_settings_button_style_box(fill_color: Color) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = fill_color
	style.border_color = Color.WHITE
	style.border_width_left = 2
	style.border_width_top = 2
	style.border_width_right = 2
	style.border_width_bottom = 2
	style.corner_radius_top_left = 8
	style.corner_radius_top_right = 8
	style.corner_radius_bottom_right = 8
	style.corner_radius_bottom_left = 8
	style.content_margin_left = 8
	style.content_margin_top = 8
	style.content_margin_right = 8
	style.content_margin_bottom = 8

	return style


## Manually positions a session button around the horizontal center of the bottom
## control area. This avoids HBox reflow when stop appears/disappears.
static func _position_session_button(control: Control, center_x: float) -> void:
	control.anchor_left = 0.5
	control.anchor_top = 0.0
	control.anchor_right = 0.5
	control.anchor_bottom = 0.0
	control.offset_left = center_x - SESSION_BUTTON_SIZE.x / 2.0
	control.offset_top = SESSION_BUTTON_CENTER_Y - SESSION_BUTTON_SIZE.y / 2.0
	control.offset_right = center_x + SESSION_BUTTON_SIZE.x / 2.0
	control.offset_bottom = SESSION_BUTTON_CENTER_Y + SESSION_BUTTON_SIZE.y / 2.0
	control.size = SESSION_BUTTON_SIZE


static func _inset_control(control: Control, left: float, top: float, right: float, bottom: float) -> void:
	control.anchor_left = 0.0
	control.anchor_top = 0.0
	control.anchor_right = 1.0
	control.anchor_bottom = 1.0
	control.offset_left = left
	control.offset_top = top
	control.offset_right = -right
	control.offset_bottom = -bottom


func _create_vertical_spacer(height: float) -> Control:
	var spacer := Control.new()
	spacer.custom_minimum_size = Vector2(0, height)
	return spacer


func _decrease_inhale_duration() -> void:
	_adjust_inhale_duration(-BreathingSettings.DURATION_STEP)


func _increase_inhale_duration() -> void:
	_adjust_inhale_duration(BreathingSettings.DURATION_STEP)


func _decrease_exhale_duration() -> void:
	_adjust_exhale_duration(-BreathingSettings.DURATION_STEP)


func _increase_exhale_duration() -> void:
	_adjust_exhale_duration(BreathingSettings.DURATION_STEP)


func _adjust_inhale_duration(delta: float) -> void:
	_draft_inhale_duration = BreathingSettings.clamp_duration(_draft_inhale_duration + delta)
	_apply_current_settings_immediately()


func _adjust_exhale_duration(delta: float) -> void:
	_draft_exhale_duration = BreathingSettings.clamp_duration(_draft_exhale_duration + delta)
	_apply_current_settings_immediately()


func _on_session_duration_slider_value_changed(value: float) -> void:
	if _is_updating_settings_controls:
		return

	var minutes := BreathingSettings.clamp_session_duration_minutes(int(roundf(value)))
	_draft_session_duration_minutes = minutes

	if _session_duration_slider != null and absf(float(_session_duration_slider.value) - float(minutes)) > 0.001:
		_is_updating_settings_controls = true
		_session_duration_slider.value = minutes
		_is_updating_settings_controls = false

	_apply_current_settings_immediately()


func _select_previous_theme() -> void:
	_draft_theme_index = _wrap_theme_index(_draft_theme_index - 1)
	_apply_current_settings_immediately()


func _select_next_theme() -> void:
	_draft_theme_index = _wrap_theme_index(_draft_theme_index + 1)
	_apply_current_settings_immediately()


## Applies draft settings to the active settings object and saves them right away.
## Timing changes reset the current session so the gauge never continues with a
## half-old, half-new rhythm.
func _apply_current_settings_immediately() -> void:
	var timing_changed := (
		absf(_settings.inhale_duration - _draft_inhale_duration) > 0.001 or
		absf(_settings.exhale_duration - _draft_exhale_duration) > 0.001 or
		_settings.session_duration_minutes != _draft_session_duration_minutes
	)

	_settings.inhale_duration = _draft_inhale_duration
	_settings.exhale_duration = _draft_exhale_duration
	_settings.session_duration_minutes = _draft_session_duration_minutes
	_settings.apply_theme(_draft_theme_index)

	if timing_changed:
		_reset_session_progress()

	SettingsStorage.save_settings(_settings)
	_apply_colors()
	_update_texts()


## Copies active settings into the draft values shown by the settings controls.
func _reset_draft_settings_from_current() -> void:
	_draft_inhale_duration = _settings.inhale_duration
	_draft_exhale_duration = _settings.exhale_duration
	_draft_session_duration_minutes = _settings.session_duration_minutes
	_draft_theme_index = _settings.current_theme_index


static func _wrap_theme_index(value: int) -> int:
	var length := BreathingSettings.THEMES.size()
	if length <= 0:
		return 0

	var result := value % length
	if result < 0:
		result += length
	return result


## Starts a new session or resumes a paused one.
func _start_or_resume_breathing_session() -> void:
	if not _has_session_started:
		_reset_session_progress()

	_enable_screen_keep_on_for_session()

	_has_session_started = true
	_is_running = true
	_update_main_screen_visibility()
	_update_texts()


## Pauses the current session without resetting elapsed time.
func _pause_breathing_session() -> void:
	if not _has_session_started or not _is_running:
		return

	_is_running = false
	_update_main_screen_visibility()
	_update_texts()


## Handles a natural session end: fade to black, reset while hidden, show the
## completion message briefly, then fade back to the initial screen.
func _complete_breathing_session() -> void:
	if _is_showing_completion:
		return

	_is_showing_completion = true
	_is_running = false
	_restore_screen_keep_on_state()
	_pause_touch_area.visible = false
	_pause_touch_area.mouse_filter = Control.MOUSE_FILTER_IGNORE

	_completion_overlay.visible = true
	_completion_overlay.mouse_filter = Control.MOUSE_FILTER_STOP
	_completion_fade.modulate = Color(1.0, 1.0, 1.0, 0.0)
	_completion_label.modulate = Color(1.0, 1.0, 1.0, 0.0)

	var fade_out := create_tween()
	fade_out.tween_property(_completion_fade, "modulate:a", 1.0, 0.45)
	await fade_out.finished

	# Reset the app while the screen is black, so the user does not see a sudden
	# jump from the final breathing frame back to the start state.
	_has_session_started = false
	_reset_session_progress()
	_update_main_screen_visibility()
	_update_texts()

	var text_fade_in := create_tween()
	text_fade_in.tween_property(_completion_label, "modulate:a", 1.0, 0.35)
	await text_fade_in.finished

	await get_tree().create_timer(2.0).timeout

	var fade_in := create_tween()
	fade_in.set_parallel(true)
	fade_in.tween_property(_completion_label, "modulate:a", 0.0, 0.35)
	fade_in.tween_property(_completion_fade, "modulate:a", 0.0, 0.45)
	await fade_in.finished

	_completion_overlay.visible = false
	_completion_overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_is_showing_completion = false


## Stops the session immediately and returns to the initial waiting state.
func _stop_breathing_session() -> void:
	_has_session_started = false
	_is_running = false
	_restore_screen_keep_on_state()
	_reset_session_progress()
	_update_main_screen_visibility()
	_update_texts()


## Opens settings from the initial screen. Any running session is explicitly
## stopped to keep settings changes simple and deterministic.
func _show_settings_screen() -> void:
	# Settings are currently available only before a breathing session starts. If
	# this is called from a future UI path, stop the session explicitly.
	_has_session_started = false
	_is_running = false
	_restore_screen_keep_on_state()
	_reset_session_progress()
	_reset_draft_settings_from_current()
	_update_main_screen_visibility()
	_update_texts()

	_main_screen.visible = false
	_settings_screen.visible = true


## Returns from settings to the breathing screen.
func _show_main_screen() -> void:
	_settings_screen.visible = false
	_main_screen.visible = true
	_update_main_screen_visibility()
	_update_texts()


## Prevents the phone from sleeping during a breathing session. The previous
## setting is saved so it can be restored exactly afterward.
func _enable_screen_keep_on_for_session() -> void:
	if _is_keeping_screen_on_for_session:
		return

	_was_screen_kept_on_before_session = DisplayServer.screen_is_kept_on()
	DisplayServer.screen_set_keep_on(true)
	_is_keeping_screen_on_for_session = true


## Restores the device keep-screen-on flag to its pre-session value.
func _restore_screen_keep_on_state() -> void:
	if not _is_keeping_screen_on_for_session:
		return

	DisplayServer.screen_set_keep_on(_was_screen_kept_on_before_session)
	_is_keeping_screen_on_for_session = false


func _reset_session_progress() -> void:
	_session_elapsed = 0.0
	_reset_cycle()


func _reset_cycle() -> void:
	_current_phase = BreathingPhase.INHALE
	_phase_elapsed = 0.0
	_update_gauge()
	_update_pause_progress_display()
	_update_texts()


## While running, any tap on the transparent pause area pauses the session.
## This is friendlier on mobile than asking the user to hit a small button.
func _on_pause_touch_area_gui_input(input_event: InputEvent) -> void:
	if not _is_primary_press(input_event):
		return

	_pause_breathing_session()
	get_viewport().set_input_as_handled()


## Accept both mobile touches and desktop left-clicks so the same code works in
## Android exports and in the Godot editor.
static func _is_primary_press(input_event: InputEvent) -> bool:
	if input_event is InputEventScreenTouch:
		var touch := input_event as InputEventScreenTouch
		return touch.pressed

	if input_event is InputEventMouseButton:
		var mouse := input_event as InputEventMouseButton
		return mouse.pressed and mouse.button_index == MOUSE_BUTTON_LEFT

	return false


func _get_current_phase_duration() -> float:
	return _settings.inhale_duration if _current_phase == BreathingPhase.INHALE else _settings.exhale_duration


## Normalized progress inside the current inhale/exhale phase, from 0 to 1.
func _get_current_phase_progress() -> float:
	return clampf(_phase_elapsed / _get_current_phase_duration(), 0.0, 1.0)


func _get_session_duration() -> float:
	return _settings.get_session_duration_seconds()


## Normalized progress of the whole session, from 0 to 1.
func _get_session_progress() -> float:
	var session_duration := _get_session_duration()
	return clampf(_session_elapsed / session_duration, 0.0, 1.0) if session_duration > 0.0 else 0.0


## Formats elapsed time for the pause screen, for example 1:05.
static func _format_minutes_seconds(total_seconds: float) -> String:
	var seconds := maxi(0, int(floorf(total_seconds)))
	var minutes := int(seconds / 60)
	return "%d:%02d" % [minutes, seconds % 60]


static func _ease_in_out(value: float) -> float:
	var t := clampf(value, 0.0, 1.0)

	# Smootherstep: starts and ends with a very low speed, while keeping the middle
	# of the movement fluid. This feels more organic than a hard pause.
	return t * t * t * (t * (t * 6.0 - 15.0) + 10.0)


## Refreshes every label from the current draft/settings values and localization.
func _update_texts() -> void:
	if _completion_label != null:
		_completion_label.text = AppLocalization.translate(AppLocalization.SESSION_COMPLETED)

	if _inhale_value_label != null:
		_inhale_value_label.text = "%.1fs" % _draft_inhale_duration

	if _exhale_value_label != null:
		_exhale_value_label.text = "%.1fs" % _draft_exhale_duration

	if _session_duration_value_label != null:
		_session_duration_value_label.text = AppLocalization.format_session_duration_minutes(_draft_session_duration_minutes)

	if _session_duration_slider != null and absf(float(_session_duration_slider.value) - float(_draft_session_duration_minutes)) > 0.001:
		_is_updating_settings_controls = true
		_session_duration_slider.value = _draft_session_duration_minutes
		_is_updating_settings_controls = false

	if _theme_label != null:
		_theme_label.text = AppLocalization.translate(BreathingSettings.THEMES[_draft_theme_index]["name_key"])

	_update_pause_progress_display()


## Shows or hides controls according to the three session states: waiting,
## running or paused. Layout containers remain visible to prevent UI jumps.
func _update_main_screen_visibility() -> void:
	var is_start_screen := not _has_session_started
	var is_paused_session := _has_session_started and not _is_running
	var is_running_session := _has_session_started and _is_running

	# The top and bottom rows stay visible even when their buttons are hidden. This
	# prevents the gauge from changing size when the session starts.
	_top_action_row.visible = true
	_bottom_controls.visible = true

	_settings_button.visible = is_start_screen
	_stop_button.visible = is_paused_session
	_start_resume_button.visible = is_start_screen or is_paused_session

	_pause_elapsed_label.visible = is_paused_session
	var parent_item := _pause_progress_bar.get_parent()
	if parent_item is CanvasItem:
		var parent_canvas_item := parent_item as CanvasItem
		parent_canvas_item.visible = is_paused_session

	# While running, this invisible Control catches any screen tap so the user can
	# pause without aiming for a tiny button.
	_pause_touch_area.visible = is_running_session
	_pause_touch_area.mouse_filter = Control.MOUSE_FILTER_STOP if is_running_session else Control.MOUSE_FILTER_IGNORE

	_update_pause_progress_display()


## Converts breathing phase progress into visual gauge progress.
func _update_gauge() -> void:
	var phase_progress := _get_current_phase_progress()
	var eased_progress := _ease_in_out(phase_progress)

	# The drawing code expects 0 at the bottom and 1 at the top.
	# Inhale climbs from 0 to 1; exhale descends from 1 to 0.
	# Easing makes the ball slow down naturally near both ends.
	var visual_progress := eased_progress if _current_phase == BreathingPhase.INHALE else 1.0 - eased_progress

	_gauge.set_progress(visual_progress)


## Refreshes the paused-session elapsed-time label and progress bar.
func _update_pause_progress_display() -> void:
	if _pause_elapsed_label == null or _pause_progress_bar == null or _pause_progress_fill == null:
		return

	if not _has_session_started:
		_pause_elapsed_label.text = ""
		_pause_progress_fill.offset_right = 0.0
		return

	var session_duration := _get_session_duration()
	var session_progress := _get_session_progress()

	_pause_elapsed_label.text = "%s / %s" % [
		_format_minutes_seconds(_session_elapsed),
		_format_minutes_seconds(session_duration),
	]

	var fill_width := _pause_progress_bar.size.x * session_progress
	_pause_progress_fill.offset_right = fill_width


## Applies the selected theme to the breathing screen. Settings remain neutral
## black/white for readability regardless of the active theme.
func _apply_colors() -> void:
	_background.color = _settings.background_color
	_gauge.gauge_color = _settings.gauge_color
	_gauge.gauge_border_color = _settings.gauge_border_color
	_gauge.ball_color = _settings.ball_color
	_gauge.queue_redraw()

	# Keep the breathing screen theme-aware, but leave the settings screen in a
	# neutral black-and-white style for consistent readability.
	_apply_text_color_recursive(_main_screen, _settings.text_color)
	_apply_text_color_recursive(_settings_screen, Color.WHITE)

	_apply_main_button_style(_settings_button, _settings.text_color)
	_apply_main_button_style(_start_resume_button, _settings.text_color, 2.0)
	_apply_main_button_style(_stop_button, _settings.text_color)


## Recursively applies text color to labels and buttons below a UI subtree.
static func _apply_text_color_recursive(node: Node, color: Color) -> void:
	if node is Label:
		var label := node as Label
		label.add_theme_color_override("font_color", color)
	elif node is Button:
		var button := node as Button
		button.add_theme_color_override("font_color", color)
		button.add_theme_color_override("font_hover_color", color)
		button.add_theme_color_override("font_pressed_color", color)

	for child in node.get_children():
		_apply_text_color_recursive(child, color)


## Anchors a Control to fill its parent. Used heavily because the UI is created
## programmatically instead of being laid out in the scene editor.
static func _fill_parent(control: Control) -> void:
	control.anchor_left = 0.0
	control.anchor_top = 0.0
	control.anchor_right = 1.0
	control.anchor_bottom = 1.0
	control.offset_left = 0.0
	control.offset_top = 0.0
	control.offset_right = 0.0
	control.offset_bottom = 0.0
