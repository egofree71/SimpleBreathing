using Godot;
using System;

/// <summary>
/// In-memory settings for one breathing session.
/// </summary>
/// <remarks>
/// Settings are centralized here and persisted by <see cref="SettingsStorage"/>.
/// Keeping the values in one model avoids spreading duration and theme state
/// through the UI code.
/// </remarks>
public sealed class BreathingSettings
{
    public const double DurationStep = 0.5;
    public const double MinimumDuration = 1.0;
    public const double MaximumDuration = 20.0;

    public const int SessionDurationStepMinutes = 1;
    public const int MinimumSessionDurationMinutes = 1;
    public const int MaximumSessionDurationMinutes = 60;

    public double InhaleDuration { get; set; } = 4.0;
    public double ExhaleDuration { get; set; } = 4.0;
    public int SessionDurationMinutes { get; set; } = 5;

    public double SessionDurationSeconds => SessionDurationMinutes * 60.0;

    public int CurrentThemeIndex { get; private set; }

    public Color BackgroundColor { get; private set; }
    public Color TextColor { get; private set; }
    public Color GaugeColor { get; private set; }
    public Color GaugeBorderColor { get; private set; }
    public Color BallColor { get; private set; }

    // Static theme list used by the settings screen. Each theme currently defines
    // all app colors directly; there is no separate Godot Theme resource yet.
    public static readonly BreathingTheme[] Themes =
    {
        new(
            AppLocalization.ThemeOcean,
            new Color(0.00f, 0.07f, 0.18f),
            new Color(0.86f, 0.96f, 1.00f),
            new Color(0.00f, 0.24f, 0.48f),
            new Color(0.00f, 0.55f, 0.95f),
            new Color(0.00f, 0.78f, 1.00f)),

        new(
            AppLocalization.ThemeJungle,
            // Inspired by the Color Hex palette 24608:
            // #63FF00, #00DD3B, #06B400, #008D02, #066916.
            new Color(0.02f, 0.41f, 0.09f),
            new Color(0.95f, 1.00f, 0.92f),
            new Color(0.00f, 0.55f, 0.01f),
            new Color(0.00f, 0.87f, 0.23f),
            new Color(0.39f, 1.00f, 0.00f)),

        new(
            AppLocalization.ThemeVolcano,
            // Inspired by the lava palette:
            // #370617, #6A040F, #9D0208, #D00000, #DC2F02, #E85D04, #F48C06, #FAA307, #FFBA08.
            new Color(0.22f, 0.02f, 0.09f),
            new Color(1.00f, 0.73f, 0.03f),
            new Color(0.62f, 0.01f, 0.03f),
            new Color(0.96f, 0.55f, 0.02f),
            new Color(1.00f, 0.73f, 0.03f)),

        new(
            AppLocalization.ThemeSky,
            new Color(0.78f, 0.92f, 1.00f),
            new Color(0.04f, 0.16f, 0.32f),
            new Color(0.96f, 0.99f, 1.00f),
            new Color(0.42f, 0.76f, 1.00f),
            new Color(0.18f, 0.62f, 1.00f))
    };

    public BreathingSettings()
    {
        ApplyTheme(0);
    }

    public string CurrentThemeNameKey => Themes[CurrentThemeIndex].NameKey;

    public void MoveToNextTheme()
    {
        ApplyTheme(CurrentThemeIndex + 1);
    }

    public void MoveToPreviousTheme()
    {
        ApplyTheme(CurrentThemeIndex - 1);
    }

    public void ApplyTheme(int themeIndex)
    {
        CurrentThemeIndex = WrapIndex(themeIndex, Themes.Length);
        BreathingTheme theme = Themes[CurrentThemeIndex];

        BackgroundColor = theme.BackgroundColor;
        TextColor = theme.TextColor;
        GaugeColor = theme.GaugeColor;
        GaugeBorderColor = theme.GaugeBorderColor;
        BallColor = theme.BallColor;
    }

    public static double ClampDuration(double value)
    {
        return Math.Clamp(value, MinimumDuration, MaximumDuration);
    }

    public static int ClampSessionDurationMinutes(int value)
    {
        return Math.Clamp(value, MinimumSessionDurationMinutes, MaximumSessionDurationMinutes);
    }

    private static int WrapIndex(int value, int length)
    {
        if (length <= 0)
        {
            return 0;
        }

        // C# '%' keeps the sign of the left operand, so negative indexes need the
        // extra correction to wrap backward through the theme list.
        int result = value % length;
        return result < 0 ? result + length : result;
    }
}

/// <summary>
/// Immutable color palette used by the breathing UI. The name is stored as a localization key.
/// </summary>
public sealed class BreathingTheme
{
    public BreathingTheme(
        string nameKey,
        Color backgroundColor,
        Color textColor,
        Color gaugeColor,
        Color gaugeBorderColor,
        Color ballColor)
    {
        NameKey = nameKey;
        BackgroundColor = backgroundColor;
        TextColor = textColor;
        GaugeColor = gaugeColor;
        GaugeBorderColor = gaugeBorderColor;
        BallColor = ballColor;
    }

    public string NameKey { get; }
    public Color BackgroundColor { get; }
    public Color TextColor { get; }
    public Color GaugeColor { get; }
    public Color GaugeBorderColor { get; }
    public Color BallColor { get; }
}
