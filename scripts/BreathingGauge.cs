using Godot;
using System;

/// <summary>
/// Custom Control that draws the breathing gauge and the moving ball.
/// </summary>
/// <remarks>
/// The gauge is drawn procedurally so the shape can react to the available mobile
/// screen size without requiring image assets. The current visual style is a simple
/// vertical capsule: one rectangle plus two circles.
/// </remarks>
public partial class BreathingGauge : Control
{
    // Normalized ball position: 0 = bottom, 1 = top.
    private float _progress;

    public Color GaugeColor { get; set; } = new(0.15f, 0.22f, 0.30f);

    // Kept for now because themes still define it. The clean gauge style no longer
    // draws a visible border, but keeping the property avoids unnecessary theme churn.
    public Color GaugeBorderColor { get; set; } = new(0.36f, 0.48f, 0.62f);

    public Color BallColor { get; set; } = new(0.43f, 0.78f, 0.98f);

    /// <summary>
    /// Updates the ball position in the gauge.
    /// </summary>
    /// <param name="progress">0 = ball at the bottom, 1 = ball at the top.</param>
    public void SetProgress(float progress)
    {
        _progress = Mathf.Clamp(progress, 0.0f, 1.0f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float width = Size.X;
        float height = Size.Y;

        if (width <= 1.0f || height <= 1.0f)
        {
            return;
        }

        // The control can stretch to fill the screen. These values define the
        // actual capsule inside the available drawing area.
        float gaugeHeight = Math.Max(120.0f, height - 30.0f);
        float gaugeWidth = Mathf.Clamp(width * 0.22f, 54.0f, 96.0f);
        float left = (width - gaugeWidth) * 0.5f;
        float top = (height - gaugeHeight) * 0.5f;

        var gaugeRect = new Rect2(left, top, gaugeWidth, gaugeHeight);

        DrawCapsuleGauge(gaugeRect);

        // The ball almost fills the gauge width, leaving a small margin so the
        // movement still feels contained rather than clipped.
        float ballRadius = Mathf.Clamp(gaugeWidth * 0.42f, 22.0f, 40.0f);
        float verticalPadding = 4.0f;

        // Keep the ball fully inside the capsule at both ends of the movement.
        float usableTop = gaugeRect.Position.Y + ballRadius + verticalPadding;
        float usableBottom = gaugeRect.Position.Y + gaugeRect.Size.Y - ballRadius - verticalPadding;
        float ballY = Mathf.Lerp(usableBottom, usableTop, _progress);
        var ballCenter = new Vector2(width * 0.5f, ballY);

        DrawCircle(ballCenter, ballRadius, BallColor);
    }

    private void DrawCapsuleGauge(Rect2 rect)
    {
        float radius = rect.Size.X * 0.5f;
        float centerX = rect.Position.X + radius;
        float topCenterY = rect.Position.Y + radius;
        float bottomCenterY = rect.Position.Y + rect.Size.Y - radius;

        // Filled capsule: central rectangle plus one circle at each end.
        // No border and no side markers are drawn in the current clean style.
        var bodyRect = new Rect2(
            rect.Position.X,
            topCenterY,
            rect.Size.X,
            Math.Max(0.0f, bottomCenterY - topCenterY));

        DrawRect(bodyRect, GaugeColor, filled: true);
        DrawCircle(new Vector2(centerX, topCenterY), radius, GaugeColor);
        DrawCircle(new Vector2(centerX, bottomCenterY), radius, GaugeColor);
    }
}
