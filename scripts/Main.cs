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

    // Main-screen controls. The containers stay visible to keep the gauge size stable.
    private Button _settingsButton = null!;
    private Button _startResumeButton = null!;
    private Button _stopButton = null!;
    private Control _topActionRow = null!;
    private Control _bottomControls = null!;

    // Transparent full-screen touch area used only while the breathing session is running.
    private Control _pauseTouchArea = null!;

    // Full-screen background color rectangle used instead of relying on theme defaults.
    private ColorRect _background = null!;

    // Two top-level screen containers. Only one is visible at a time.
    private Control _mainScreen = null!;
    private Control _settingsScreen = null!;

    // Current breathing phase and elapsed time inside that phase.
    private BreathingPhase _currentPhase = BreathingPhase.Inhale;
    private double _phaseElapsed;

    // A session starts only after the user presses the start button.
    private bool _hasSessionStarted;

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
        UpdateMainScreenVisibility();
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
    /// Builds the calm breathing screen.
    /// </summary>
    /// <remarks>
    /// The start screen shows a settings button at the top left and a start icon
    /// at the bottom center. During an active session, all buttons are hidden so
    /// the user only sees the gauge and moving ball. Tapping the screen pauses the
    /// session and reveals the stop/resume icons.
    /// </remarks>
    private Control BuildMainScreen()
    {
        var root = new Control
        {
            Name = "MainScreen"
        };

        var margin = new MarginContainer
        {
            Name = "MainScreenMargin"
        };
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        root.AddChild(margin);
        FillParent(margin);

        var mainColumn = new VBoxContainer
        {
            Name = "MainColumn",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        mainColumn.AddThemeConstantOverride("separation", 8);
        margin.AddChild(mainColumn);

        // Keep the settings button as an overlay instead of placing it in the
        // vertical layout. This lets the gauge sit slightly higher on the screen.
        _topActionRow = BuildTopActionRow();
        root.AddChild(_topActionRow);

        // Small top spacer: enough breathing room from the top edge, but less than
        // the full settings-button row used previously.
        mainColumn.AddChild(CreateVerticalSpacer(24));

        _gauge = new BreathingGauge
        {
            Name = "BreathingGauge",
            CustomMinimumSize = new Vector2(260, 520),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        mainColumn.AddChild(_gauge);

        _bottomControls = BuildBottomControls();
        mainColumn.AddChild(_bottomControls);

        _pauseTouchArea = new Control
        {
            Name = "PauseTouchArea",
            MouseFilter = MouseFilterEnum.Stop,
            Visible = false
        };
        _pauseTouchArea.GuiInput += OnPauseTouchAreaGuiInput;
        root.AddChild(_pauseTouchArea);
        FillParent(_pauseTouchArea);

        return root;
    }

    private Control BuildTopActionRow()
    {
        var row = new HBoxContainer
        {
            Name = "TopActionRow",
            Position = new Vector2(20, 16),
            Size = new Vector2(64, 56),
            CustomMinimumSize = new Vector2(64, 56)
        };
        row.AddThemeConstantOverride("separation", 12);

        _settingsButton = CreateIconButton("⚙", ShowSettingsScreen, 34);
        row.AddChild(_settingsButton);

        row.AddChild(new Control
        {
            Name = "TopActionSpacer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });

        return row;
    }

    private Control BuildBottomControls()
    {
        var controls = new Control
        {
            Name = "BottomControls",
            CustomMinimumSize = new Vector2(0, 64),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        _stopButton = CreateSessionIconButton("■", StopBreathingSession, 34);
        controls.AddChild(_stopButton);

        _startResumeButton = CreateSessionIconButton("▶", StartOrResumeBreathingSession, 38);
        controls.AddChild(_startResumeButton);

        // The resume button is anchored to the exact horizontal center. When the
        // stop button appears on the left during pause, the resume button does not
        // shift: it stays directly below the gauge.
        PositionControl(_startResumeButton, left: -38, top: 2, right: 38, bottom: 62);
        PositionControl(_stopButton, left: -130, top: 2, right: -54, bottom: 62);

        return controls;
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

        var colorsTitle = CreateSectionTitle("Thèmes");
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
            CustomMinimumSize = new Vector2(112, 48)
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

    private Button CreateSessionIconButton(string text, Action onPressed, int fontSize)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(76, 60)
        };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.Pressed += onPressed;
        return button;
    }

    private static void PositionControl(Control control, float left, float top, float right, float bottom)
    {
        control.AnchorLeft = 0.5f;
        control.AnchorTop = 0.0f;
        control.AnchorRight = 0.5f;
        control.AnchorBottom = 0.0f;
        control.OffsetLeft = left;
        control.OffsetTop = top;
        control.OffsetRight = right;
        control.OffsetBottom = bottom;
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

    private void StartOrResumeBreathingSession()
    {
        _hasSessionStarted = true;
        _isRunning = true;
        UpdateMainScreenVisibility();
        UpdateTexts();
    }

    private void PauseBreathingSession()
    {
        if (!_hasSessionStarted || !_isRunning)
        {
            return;
        }

        _isRunning = false;
        UpdateMainScreenVisibility();
        UpdateTexts();
    }

    private void StopBreathingSession()
    {
        _hasSessionStarted = false;
        _isRunning = false;
        ResetCycle();
        UpdateMainScreenVisibility();
        UpdateTexts();
    }

    private void ShowSettingsScreen()
    {
        // Settings are currently available only before a breathing session starts.
        // If this is called from a future UI path, stop the session explicitly.
        _hasSessionStarted = false;
        _isRunning = false;
        UpdateMainScreenVisibility();
        UpdateTexts();

        _mainScreen.Visible = false;
        _settingsScreen.Visible = true;
    }

    private void ShowMainScreen()
    {
        _settingsScreen.Visible = false;
        _mainScreen.Visible = true;
        UpdateMainScreenVisibility();
        UpdateTexts();
    }

    private void ResetCycle()
    {
        _currentPhase = BreathingPhase.Inhale;
        _phaseElapsed = 0.0;
        UpdateGauge();
        UpdateTexts();
    }

    private void OnPauseTouchAreaGuiInput(InputEvent inputEvent)
    {
        if (!IsPrimaryPress(inputEvent))
        {
            return;
        }

        PauseBreathingSession();
        GetViewport().SetInputAsHandled();
    }

    private static bool IsPrimaryPress(InputEvent inputEvent)
    {
        if (inputEvent is InputEventScreenTouch touch)
        {
            return touch.Pressed;
        }

        if (inputEvent is InputEventMouseButton mouse)
        {
            return mouse.Pressed && mouse.ButtonIndex == MouseButton.Left;
        }

        return false;
    }

    private double GetCurrentPhaseDuration()
    {
        return _currentPhase == BreathingPhase.Inhale
            ? _settings.InhaleDuration
            : _settings.ExhaleDuration;
    }

    private void UpdateTexts()
    {
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

    private void UpdateMainScreenVisibility()
    {
        bool isStartScreen = !_hasSessionStarted;
        bool isPausedSession = _hasSessionStarted && !_isRunning;
        bool isRunningSession = _hasSessionStarted && _isRunning;

        // The top and bottom rows stay visible even when their buttons are hidden.
        // This prevents the gauge from changing size when the session starts.
        _topActionRow.Visible = true;
        _bottomControls.Visible = true;

        _settingsButton.Visible = isStartScreen;
        _stopButton.Visible = isPausedSession;
        _startResumeButton.Visible = isStartScreen || isPausedSession;

        // While running, this invisible Control catches any screen tap so the
        // user can pause without aiming for a tiny button.
        _pauseTouchArea.Visible = isRunningSession;
        _pauseTouchArea.MouseFilter = isRunningSession
            ? MouseFilterEnum.Stop
            : MouseFilterEnum.Ignore;
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
