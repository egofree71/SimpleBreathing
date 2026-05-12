using Godot;
using System;

/// <summary>
/// Main application controller.
/// </summary>
/// <remarks>
/// The current UI is intentionally built from code rather than from a detailed
/// Godot scene tree. This keeps early mobile layout iterations quick: the scene
/// only provides the root Control node, while this class creates the main screen,
/// the settings screen, and wires the breathing cycle.
/// </remarks>
public partial class Main : Control
{
    private enum BreathingPhase
    {
        Inhale,
        Exhale
    }

    // In-memory breathing durations and color theme selection.
    private readonly BreathingSettings _settings = new();

    // Custom Control responsible for drawing the gauge and moving ball.
    private BreathingGauge _gauge = null!;

    // Settings-screen labels refreshed whenever durations or theme change.
    private Label _inhaleValueLabel = null!;
    private Label _exhaleValueLabel = null!;
    private Label _themeLabel = null!;

    // Main action button. Its text switches between play and pause icons.
    private Button _startPauseButton = null!;

    // Full-screen background color rectangle used instead of relying on theme defaults.
    private ColorRect _background = null!;

    // Two top-level screen containers. Only one is visible at a time.
    private Control _mainScreen = null!;
    private Control _settingsScreen = null!;

    // Current breathing phase and elapsed time inside that phase.
    private BreathingPhase _currentPhase = BreathingPhase.Inhale;
    private double _phaseElapsed;

    // False on startup: the ball is visible at the bottom, but the cycle is not moving yet.
    private bool _isRunning;

    /// <summary>
    /// Builds the runtime UI and initializes visuals once the scene enters the tree.
    /// </summary>
    public override void _Ready()
    {
        BuildInterface();
        ApplyColors();
        UpdateTexts();
        UpdateGauge();
    }

    /// <summary>
    /// Advances the breathing cycle while the application is running.
    /// </summary>
    public override void _Process(double delta)
    {
        if (!_isRunning)
        {
            return;
        }

        _phaseElapsed += delta;

        // Use a while loop instead of a single if so the cycle remains valid even
        // after a large frame hitch, for example when the app is resumed on mobile.
        while (_phaseElapsed >= GetCurrentPhaseDuration())
        {
            _phaseElapsed -= GetCurrentPhaseDuration();
            _currentPhase = _currentPhase == BreathingPhase.Inhale
                ? BreathingPhase.Exhale
                : BreathingPhase.Inhale;
        }

        UpdateGauge();
    }

    /// <summary>
    /// Creates the shared background and both application screens.
    /// </summary>
    private void BuildInterface()
    {
        _background = new ColorRect
        {
            Name = "Background",
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_background);
        FillParent(_background);

        _mainScreen = BuildMainScreen();
        AddChild(_mainScreen);
        FillParent(_mainScreen);

        _settingsScreen = BuildSettingsScreen();
        _settingsScreen.Visible = false;
        AddChild(_settingsScreen);
        FillParent(_settingsScreen);
    }

    /// <summary>
    /// Builds the calm breathing screen: gauge only, with play/pause and settings buttons.
    /// </summary>
    private Control BuildMainScreen()
    {
        var margin = new MarginContainer
        {
            Name = "MainScreen"
        };
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);

        var mainColumn = new VBoxContainer
        {
            Name = "MainColumn",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        mainColumn.AddThemeConstantOverride("separation", 18);
        margin.AddChild(mainColumn);

        _gauge = new BreathingGauge
        {
            Name = "BreathingGauge",
            CustomMinimumSize = new Vector2(260, 520),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        mainColumn.AddChild(_gauge);

        var bottomRow = new HBoxContainer
        {
            Name = "BottomActionRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        bottomRow.AddThemeConstantOverride("separation", 12);
        mainColumn.AddChild(bottomRow);

        _startPauseButton = CreateIconButton("▶", ToggleBreathing, 34);
        bottomRow.AddChild(_startPauseButton);

        // Expands between the two buttons so they stay anchored to opposite sides
        // on portrait phone screens.
        var spacer = new Control
        {
            Name = "BottomSpacer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        bottomRow.AddChild(spacer);

        bottomRow.AddChild(CreateIconButton("⚙", ShowSettingsScreen, 28));

        return margin;
    }

    /// <summary>
    /// Builds the separate settings screen used to edit durations and color theme.
    /// </summary>
    private Control BuildSettingsScreen()
    {
        var margin = new MarginContainer
        {
            Name = "SettingsScreen"
        };
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);

        var column = new VBoxContainer
        {
            Name = "SettingsColumn",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        column.AddThemeConstantOverride("separation", 18);
        margin.AddChild(column);

        var headerRow = new HBoxContainer
        {
            Name = "SettingsHeader",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        headerRow.AddThemeConstantOverride("separation", 12);
        column.AddChild(headerRow);

        headerRow.AddChild(CreateIconButton("←", ShowMainScreen, 28));

        var title = new Label
        {
            Text = "Réglages",
            VerticalAlignment = Godot.VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 28);
        headerRow.AddChild(title);

        column.AddChild(CreateVerticalSpacer(10));

        var breathingTitle = CreateSectionTitle("Respiration");
        column.AddChild(breathingTitle);

        column.AddChild(CreateDurationRow(
            "Inspiration",
            out _inhaleValueLabel,
            () => AdjustInhaleDuration(-BreathingSettings.DurationStep),
            () => AdjustInhaleDuration(BreathingSettings.DurationStep)));

        column.AddChild(CreateDurationRow(
            "Expiration",
            out _exhaleValueLabel,
            () => AdjustExhaleDuration(-BreathingSettings.DurationStep),
            () => AdjustExhaleDuration(BreathingSettings.DurationStep)));

        column.AddChild(CreateVerticalSpacer(10));

        var colorsTitle = CreateSectionTitle("Couleurs");
        column.AddChild(colorsTitle);
        column.AddChild(CreateThemeRow());

        // Pushes the reset button toward the bottom without hard-coding a phone height.
        var flexibleSpacer = new Control
        {
            Name = "SettingsFlexibleSpacer",
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        column.AddChild(flexibleSpacer);

        var resetRow = new HBoxContainer
        {
            Name = "SettingsResetRow",
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        resetRow.AddChild(CreateButton("Réinitialiser le cycle", ResetCycle));
        column.AddChild(resetRow);

        return margin;
    }

    private Label CreateSectionTitle(string text)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = Godot.HorizontalAlignment.Left
        };
        label.AddThemeFontSizeOverride("font_size", 20);
        return label;
    }

    /// <summary>
    /// Creates one duration editor row with a label, decrease button, value label, and increase button.
    /// </summary>
    private HBoxContainer CreateDurationRow(string labelText, out Label valueLabel, Action decrease, Action increase)
    {
        var row = new HBoxContainer
        {
            Name = labelText + "Row",
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 10);

        var label = new Label
        {
            Text = labelText,
            CustomMinimumSize = new Vector2(120, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = Godot.VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 17);
        row.AddChild(label);

        row.AddChild(CreateButton("−", decrease));

        valueLabel = new Label
        {
            CustomMinimumSize = new Vector2(72, 0),
            HorizontalAlignment = Godot.HorizontalAlignment.Center,
            VerticalAlignment = Godot.VerticalAlignment.Center
        };
        valueLabel.AddThemeFontSizeOverride("font_size", 17);
        row.AddChild(valueLabel);

        row.AddChild(CreateButton("+", increase));

        return row;
    }

    private HBoxContainer CreateThemeRow()
    {
        var row = new HBoxContainer
        {
            Name = "ThemeRow",
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 10);

        row.AddChild(CreateButton("‹", SelectPreviousTheme));

        _themeLabel = new Label
        {
            Name = "ThemeLabel",
            CustomMinimumSize = new Vector2(180, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = Godot.HorizontalAlignment.Center,
            VerticalAlignment = Godot.VerticalAlignment.Center
        };
        _themeLabel.AddThemeFontSizeOverride("font_size", 17);
        row.AddChild(_themeLabel);

        row.AddChild(CreateButton("›", SelectNextTheme));

        return row;
    }

    private Button CreateButton(string text, Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(88, 44)
        };
        button.Pressed += onPressed;
        return button;
    }

    private Button CreateIconButton(string text, Action onPressed, int fontSize = 28)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(64, 56)
        };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.Pressed += onPressed;
        return button;
    }

    private Control CreateVerticalSpacer(float height)
    {
        return new Control
        {
            CustomMinimumSize = new Vector2(0, height)
        };
    }

    private void AdjustInhaleDuration(double delta)
    {
        _settings.InhaleDuration = BreathingSettings.ClampDuration(_settings.InhaleDuration + delta);
        ResetCycle();
        UpdateTexts();
    }

    private void AdjustExhaleDuration(double delta)
    {
        _settings.ExhaleDuration = BreathingSettings.ClampDuration(_settings.ExhaleDuration + delta);
        ResetCycle();
        UpdateTexts();
    }

    private void SelectPreviousTheme()
    {
        _settings.MoveToPreviousTheme();
        ApplyColors();
        UpdateTexts();
    }

    private void SelectNextTheme()
    {
        _settings.MoveToNextTheme();
        ApplyColors();
        UpdateTexts();
    }

    private void ToggleBreathing()
    {
        _isRunning = !_isRunning;
        UpdateTexts();
    }

    private void ShowSettingsScreen()
    {
        // Opening settings pauses the breathing animation. This avoids a moving
        // background state while the user is changing durations or colors.
        _isRunning = false;
        UpdateTexts();

        _mainScreen.Visible = false;
        _settingsScreen.Visible = true;
    }

    private void ShowMainScreen()
    {
        _settingsScreen.Visible = false;
        _mainScreen.Visible = true;
        UpdateTexts();
    }

    private void ResetCycle()
    {
        _currentPhase = BreathingPhase.Inhale;
        _phaseElapsed = 0.0;
        UpdateGauge();
        UpdateTexts();
    }

    private double GetCurrentPhaseDuration()
    {
        return _currentPhase == BreathingPhase.Inhale
            ? _settings.InhaleDuration
            : _settings.ExhaleDuration;
    }

    private void UpdateTexts()
    {
        if (_startPauseButton != null)
        {
            _startPauseButton.Text = _isRunning ? "⏸" : "▶";
        }

        if (_inhaleValueLabel != null)
        {
            _inhaleValueLabel.Text = $"{_settings.InhaleDuration:0.0}s";
        }

        if (_exhaleValueLabel != null)
        {
            _exhaleValueLabel.Text = $"{_settings.ExhaleDuration:0.0}s";
        }

        if (_themeLabel != null)
        {
            _themeLabel.Text = _settings.CurrentThemeName;
        }
    }

    private void UpdateGauge()
    {
        double phaseProgress = _phaseElapsed / GetCurrentPhaseDuration();
        phaseProgress = Math.Clamp(phaseProgress, 0.0, 1.0);

        // The drawing code expects 0 at the bottom and 1 at the top.
        // Inhale climbs from 0 to 1; exhale descends from 1 to 0.
        float visualProgress = _currentPhase == BreathingPhase.Inhale
            ? (float)phaseProgress
            : 1.0f - (float)phaseProgress;

        _gauge.SetProgress(visualProgress);
    }

    private void ApplyColors()
    {
        _background.Color = _settings.BackgroundColor;
        _gauge.GaugeColor = _settings.GaugeColor;
        _gauge.GaugeBorderColor = _settings.GaugeBorderColor;
        _gauge.BallColor = _settings.BallColor;
        _gauge.QueueRedraw();

        ApplyTextColorRecursive(this, _settings.TextColor);
    }

    /// <summary>
    /// Applies text colors after the runtime UI tree exists.
    /// </summary>
    private static void ApplyTextColorRecursive(Node node, Color color)
    {
        if (node is Label label)
        {
            label.AddThemeColorOverride("font_color", color);
        }
        else if (node is Button button)
        {
            button.AddThemeColorOverride("font_color", color);
            button.AddThemeColorOverride("font_hover_color", color);
            button.AddThemeColorOverride("font_pressed_color", color);
        }

        foreach (Node child in node.GetChildren())
        {
            ApplyTextColorRecursive(child, color);
        }
    }

    /// <summary>
    /// Anchors a Control to fill its parent.
    /// </summary>
    /// <remarks>
    /// This helper avoids repeating the four anchors and four offsets every time a
    /// screen-sized Control is created from code.
    /// </remarks>
    private static void FillParent(Control control)
    {
        control.AnchorLeft = 0.0f;
        control.AnchorTop = 0.0f;
        control.AnchorRight = 1.0f;
        control.AnchorBottom = 1.0f;
        control.OffsetLeft = 0.0f;
        control.OffsetTop = 0.0f;
        control.OffsetRight = 0.0f;
        control.OffsetBottom = 0.0f;
    }
}
