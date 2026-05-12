extends Control

## Custom Control that draws the breathing gauge and the moving ball.
##
## The gauge is drawn procedurally so the shape can react to the available mobile
## screen size without requiring image assets. The current visual style is a simple
## vertical capsule: one rectangle plus two circles.

# Normalized ball position: 0 = bottom, 1 = top.
var _progress := 0.0

var gauge_color := Color(0.15, 0.22, 0.30)

# Kept for now because themes still define it. The clean gauge style no longer
# draws a visible border, but keeping the property avoids unnecessary theme churn.
var gauge_border_color := Color(0.36, 0.48, 0.62)

var ball_color := Color(0.43, 0.78, 0.98)


## Updates the normalized ball position and requests a redraw.
func set_progress(progress: float) -> void:
	_progress = clampf(progress, 0.0, 1.0)
	queue_redraw()


## Godot draw callback. Everything is computed from the current Control size so
## the gauge adapts to desktop and phone screen dimensions.
func _draw() -> void:
	var width := size.x
	var height := size.y

	if width <= 1.0 or height <= 1.0:
		return

	# The control can stretch to fill the screen. These values define the actual
	# capsule inside the available drawing area. The capsule is kept slightly
	# smaller than before to feel lighter on a phone screen.
	var gauge_height: float = maxf(108.0, (height - 30.0) * 0.90)
	var gauge_width := clampf(width * 0.198, 49.0, 86.0)
	var left := (width - gauge_width) * 0.5
	var top := (height - gauge_height) * 0.5

	var gauge_rect := Rect2(left, top, gauge_width, gauge_height)

	_draw_capsule_gauge(gauge_rect)

	# The ball uses the full capsule width. There is intentionally no side padding:
	# the circle touches the gauge edges, like a piston in its tube.
	var ball_radius := gauge_width * 0.5
	var vertical_padding := 0.0

	# Keep the ball fully inside the capsule at both ends of the movement.
	var usable_top := gauge_rect.position.y + ball_radius + vertical_padding
	var usable_bottom := gauge_rect.position.y + gauge_rect.size.y - ball_radius - vertical_padding
	var ball_y := lerpf(usable_bottom, usable_top, _progress)
	var ball_center := Vector2(width * 0.5, ball_y)

	draw_circle(ball_center, ball_radius, ball_color)


## Draws a capsule manually using a rectangle and two circles.
func _draw_capsule_gauge(rect: Rect2) -> void:
	var radius := rect.size.x * 0.5
	var center_x := rect.position.x + radius
	var top_center_y := rect.position.y + radius
	var bottom_center_y := rect.position.y + rect.size.y - radius

	# Filled capsule: central rectangle plus one circle at each end.
	# No border and no side markers are drawn in the current clean style.
	var body_rect := Rect2(
		rect.position.x,
		top_center_y,
		rect.size.x,
		maxf(0.0, bottom_center_y - top_center_y)
	)

	draw_rect(body_rect, gauge_color, true)
	draw_circle(Vector2(center_x, top_center_y), radius, gauge_color)
	draw_circle(Vector2(center_x, bottom_center_y), radius, gauge_color)
