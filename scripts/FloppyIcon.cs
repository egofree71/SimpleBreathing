using Godot;

/// <summary>
/// Save icon displayed inside the settings save button.
/// </summary>
/// <remarks>
/// The icon uses an SVG asset instead of procedural drawing because the real
/// floppy silhouette is much more readable at small mobile button sizes.
/// The SVG is stored in white and tinted at draw time through IconColor.
/// </remarks>
public partial class FloppyIcon : Control
{
    private const string IconPath = "res://assets/icons/floppy-disk.svg";

    private Texture2D? _texture;

    public Color IconColor { get; set; } = Colors.White;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _texture = GD.Load<Texture2D>(IconPath);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        float side = Mathf.Min(Size.X, Size.Y);
        if (side <= 1.0f)
        {
            return;
        }

        // Keep a tiny internal padding so the icon does not touch the button edge.
        float iconSide = side * 0.92f;
        var rect = new Rect2(
            (Size.X - iconSide) * 0.5f,
            (Size.Y - iconSide) * 0.5f,
            iconSide,
            iconSide);

        if (_texture != null)
        {
            DrawTextureRect(_texture, rect, false, IconColor);
            return;
        }

        // Fallback in case the SVG has not been imported yet.
        DrawRect(rect, IconColor, filled: false, width: 3.0f);
        DrawRect(
            new Rect2(rect.Position + rect.Size * 0.22f, rect.Size * new Vector2(0.56f, 0.18f)),
            IconColor,
            filled: true);
        DrawRect(
            new Rect2(rect.Position + rect.Size * new Vector2(0.25f, 0.58f), rect.Size * new Vector2(0.50f, 0.22f)),
            IconColor,
            filled: true);
    }
}
