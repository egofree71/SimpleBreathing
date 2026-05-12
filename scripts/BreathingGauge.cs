using Godot;
using System;

public partial class BreathingGauge : Control
{
    private float _progress;

    public Color GaugeColor { get; set; } = new(0.15f, 0.22f, 0.30f);
    public Color GaugeBorderColor { get; set; } = new(0.36f, 0.48f, 0.62f);
    public Color BallColor { get; set; } = new(0.43f, 0.78f, 0.98f);

    /// <summary>
    /// 0 = boule en bas, 1 = boule en haut.
    /// </summary>
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

        float gaugeHeight = Math.Max(120.0f, height - 30.0f);
        float gaugeWidth = Mathf.Clamp(width * 0.22f, 54.0f, 96.0f);
        float left = (width - gaugeWidth) * 0.5f;
        float top = (height - gaugeHeight) * 0.5f;

        var gaugeRect = new Rect2(left, top, gaugeWidth, gaugeHeight);

        DrawCapsuleGauge(gaugeRect);

        // La boule prend davantage de place dans la jauge, avec des marges latérales plus petites.
        float ballRadius = Mathf.Clamp(gaugeWidth * 0.42f, 22.0f, 40.0f);
        float verticalPadding = 4.0f;
        float usableTop = gaugeRect.Position.Y + ballRadius + verticalPadding;
        float usableBottom = gaugeRect.Position.Y + gaugeRect.Size.Y - ballRadius - verticalPadding;
        float ballY = Mathf.Lerp(usableBottom, usableTop, _progress);
        var ballCenter = new Vector2(width * 0.5f, ballY);

        DrawCircle(ballCenter, ballRadius, BallColor);

        // Petits repères visuels haut/bas pour rendre la jauge plus lisible.
        float markerLeft = gaugeRect.Position.X - 14.0f;
        float markerRight = gaugeRect.Position.X - 4.0f;
        DrawLine(new Vector2(markerLeft, usableTop), new Vector2(markerRight, usableTop), GaugeBorderColor, 2.0f, true);
        DrawLine(new Vector2(markerLeft, usableBottom), new Vector2(markerRight, usableBottom), GaugeBorderColor, 2.0f, true);
    }

    private void DrawCapsuleGauge(Rect2 rect)
    {
        float radius = rect.Size.X * 0.5f;
        float centerX = rect.Position.X + radius;
        float topCenterY = rect.Position.Y + radius;
        float bottomCenterY = rect.Position.Y + rect.Size.Y - radius;

        // Remplissage : un rectangle central + deux cercles donnent une jauge en forme de capsule.
        var bodyRect = new Rect2(
            rect.Position.X,
            topCenterY,
            rect.Size.X,
            Math.Max(0.0f, bottomCenterY - topCenterY));

        DrawRect(bodyRect, GaugeColor, filled: true);
        DrawCircle(new Vector2(centerX, topCenterY), radius, GaugeColor);
        DrawCircle(new Vector2(centerX, bottomCenterY), radius, GaugeColor);

        // Bordure de la capsule.
        float borderWidth = 3.0f;
        float borderRadius = radius - borderWidth * 0.5f;
        float leftX = rect.Position.X + borderWidth * 0.5f;
        float rightX = rect.Position.X + rect.Size.X - borderWidth * 0.5f;

        DrawLine(
            new Vector2(leftX, topCenterY),
            new Vector2(leftX, bottomCenterY),
            GaugeBorderColor,
            borderWidth,
            true);

        DrawLine(
            new Vector2(rightX, topCenterY),
            new Vector2(rightX, bottomCenterY),
            GaugeBorderColor,
            borderWidth,
            true);

        DrawArc(
            new Vector2(centerX, topCenterY),
            borderRadius,
            Mathf.Pi,
            Mathf.Tau,
            48,
            GaugeBorderColor,
            borderWidth,
            true);

        DrawArc(
            new Vector2(centerX, bottomCenterY),
            borderRadius,
            0.0f,
            Mathf.Pi,
            48,
            GaugeBorderColor,
            borderWidth,
            true);
    }
}
