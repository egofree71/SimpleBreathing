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

    // Active breathing durations and color theme selection.
    private readonly BreathingSettings _settings = new();

    // Draft values edited on the settings screen. They are copied to _settings only
    // when the user presses the save button.
    private double _draftInhaleDuration;
    private double _draftExhaleDuration;
    private int _draftSessionDurationMinutes;
    private int _draftThemeIndex;

    // Avoids reacting to slider changes caused by UpdateTexts() itself.
    private bool _isUpdatingSettingsControls;

    // Custom Control responsible for drawing the gauge and moving ball.
    private BreathingGauge _gauge = null!;

    // Settings-screen labels refreshed whenever durations, session length, or theme change.
    private Label _inhaleValueLabel = null!;
    private Label _exhaleValueLabel = null!;
    private Label _sessionDurationValueLabel = null!;
    private HSlider _sessionDurationSlider = null!;
    private Label _themeLabel = null!;

    // Main-screen controls. The containers stay visible to keep the gauge size stable.
    private Button _settingsButton = null!;
    private Button _startResumeButton = null!;
    private Button _stopButton = null!;
    private Control _topActionRow = null!;
    private Control _bottomControls = null!;

    // Pause-only information shown above the gauge.
    private Label _pauseElapsedLabel = null!;
    private Control _pauseProgressBar = null!;
    private ColorRect _pauseProgressFill = null!;

    // Transparent full-screen touch area used only while the breathing session is running.
    private Control _pauseTouchArea = null!;

    // Full-screen background color rectangle used instead of relying on theme defaults.
    private ColorRect _background = null!;

    // Two top-level screen containers. Only one is visible at a time.
    private Control _mainScreen = null!;
    private Control _settingsScreen = null!;

    // Current breathing phase, elapsed time inside that phase, and total elapsed session time.
    private BreathingPhase _currentPhase = BreathingPhase.Inhale;
    private double _phaseElapsed;
    private double _sessionElapsed;

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
        ResetDraftSettingsFromCurrent();
        ApplyColors();
        UpdateTexts();
        UpdateGauge();
        UpdatePauseProgressDisplay();
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

        _sessionElapsed += delta;

        if (_sessionElapsed >= GetSessionDuration())
        {
            StopBreathingSession();
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
        UpdatePauseProgressDisplay();
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
        // vertical layout. This prevents it from pushing the gauge downward.
        _topActionRow = BuildTopActionRow();
        root.AddChild(_topActionRow);

        // The pause progress area and the bottom controls have the same reserved
        // height, so the gauge is vertically centered in the screen.

        // This fixed-height area stays in the layout all the time so the gauge does
        // not jump when the pause information appears.
        mainColumn.AddChild(BuildPauseProgressArea());

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

    private Control BuildPauseProgressArea()
    {
        var area = new Control
        {
            Name = "PauseProgressArea",
            CustomMinimumSize = new Vector2(0, 64),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        var column = new VBoxContainer
        {
            Name = "PauseProgressColumn",
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        column.AddThemeConstantOverride("separation", 8);
        area.AddChild(column);
        FillParent(column);
        // Move the pause progress display slightly closer to the gauge.
        column.OffsetTop = 14.0f;

        _pauseElapsedLabel = new Label
        {
            Name = "PauseElapsedLabel",
            HorizontalAlignment = Godot.HorizontalAlignment.Center,
            Visible = false
        };
        _pauseElapsedLabel.AddThemeFontSizeOverride("font_size", 18);
        column.AddChild(_pauseElapsedLabel);

        var barCenter = new CenterContainer
        {
            Name = "PauseProgressBarCenter",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Visible = false
        };
        column.AddChild(barCenter);

        _pauseProgressBar = new Control
        {
            Name = "PauseProgressBar",
            // 360px is roughly three quarters of the current 480px base viewport width.
            CustomMinimumSize = new Vector2(360, 16),
            ClipContents = true
        };
        _pauseProgressBar.Resized += UpdatePauseProgressDisplay;
        barCenter.AddChild(_pauseProgressBar);

        var background = new ColorRect
        {
            Name = "PauseProgressBarBackground",
            Color = new Color(1.0f, 1.0f, 1.0f, 0.25f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _pauseProgressBar.AddChild(background);
        FillParent(background);

        _pauseProgressFill = new ColorRect
        {
            Name = "PauseProgressBarFill",
            Color = new Color(1.0f, 1.0f, 1.0f, 0.95f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _pauseProgressBar.AddChild(_pauseProgressFill);
        _pauseProgressFill.AnchorLeft = 0.0f;
        _pauseProgressFill.AnchorTop = 0.0f;
        _pauseProgressFill.AnchorRight = 0.0f;
        _pauseProgressFill.AnchorBottom = 1.0f;
        _pauseProgressFill.OffsetLeft = 0.0f;
        _pauseProgressFill.OffsetTop = 0.0f;
        _pauseProgressFill.OffsetRight = 0.0f;
        _pauseProgressFill.OffsetBottom = 0.0f;

        return area;
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
        //
        // The buttons are moved slightly upward inside this reserved bottom area so
        // their visual center sits between the gauge and the bottom of the screen.
        PositionControl(_startResumeButton, left: -38, top: -14, right: 38, bottom: 46);
        PositionControl(_stopButton, left: -130, top: -14, right: -54, bottom: 46);

        return controls;
    }

    /// <summary>
    /// Builds the separate settings screen used to edit durations and color theme.
    /// </summary>
    private Control BuildSettingsScreen()
    {
        var root = new Control
        {
            Name = "SettingsScreenRoot"
        };

        // Keep the settings screen visually neutral: solid black background and
        // white controls, regardless of the currently selected breathing theme.
        var background = new ColorRect
        {
            Name = "SettingsBackground",
            Color = Colors.Black,
            MouseFilter = MouseFilterEnum.Ignore
        };
        root.AddChild(background);
        FillParent(background);

        var margin = new MarginContainer
        {
            Name = "SettingsScreen"
        };
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        root.AddChild(margin);
        FillParent(margin);

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

        headerRow.AddChild(CreateSettingsIconButton("←", CancelSettings, 28));

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

        column.AddChild(CreateSessionDurationSlider());

        column.AddChild(CreateVerticalSpacer(10));

        var colorsTitle = CreateSectionTitle("Thèmes");
        column.AddChild(colorsTitle);
        column.AddChild(CreateThemeRow());

        // Pushes the save button toward the bottom without hard-coding a phone height.
        var flexibleSpacer = new Control
        {
            Name = "SettingsFlexibleSpacer",
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        column.AddChild(flexibleSpacer);

        var saveRow = new HBoxContainer
        {
            Name = "SettingsSaveRow",
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        saveRow.AddChild(CreateSettingsSaveButton(SaveSettings));
        column.AddChild(saveRow);

        return root;
    }

    private Label CreateSectionTitle(string text)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = Godot.HorizontalAlignment.Left
        };
        label.AddThemeFontSizeOverride("font_size", 22);
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
        label.AddThemeFontSizeOverride("font_size", 20);
        row.AddChild(label);

        row.AddChild(CreateSettingsButton("−", decrease));

        valueLabel = new Label
        {
            CustomMinimumSize = new Vector2(72, 0),
            HorizontalAlignment = Godot.HorizontalAlignment.Center,
            VerticalAlignment = Godot.VerticalAlignment.Center
        };
        valueLabel.AddThemeFontSizeOverride("font_size", 20);
        row.AddChild(valueLabel);

        row.AddChild(CreateSettingsButton("+", increase));

        return row;
    }

    private VBoxContainer CreateSessionDurationSlider()
    {
        var container = new VBoxContainer
        {
            Name = "SessionDurationContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        container.AddThemeConstantOverride("separation", 8);

        var row = new HBoxContainer
        {
            Name = "SessionDurationRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 10);
        container.AddChild(row);

        var label = new Label
        {
            Text = "Durée de séance",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = Godot.VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 20);
        row.AddChild(label);

        _sessionDurationValueLabel = new Label
        {
            Name = "SessionDurationValueLabel",
            CustomMinimumSize = new Vector2(90, 0),
            HorizontalAlignment = Godot.HorizontalAlignment.Right,
            VerticalAlignment = Godot.VerticalAlignment.Center
        };
        _sessionDurationValueLabel.AddThemeFontSizeOverride("font_size", 20);
        row.AddChild(_sessionDurationValueLabel);

        _sessionDurationSlider = new HSlider
        {
            Name = "SessionDurationSlider",
            MinValue = BreathingSettings.MinimumSessionDurationMinutes,
            MaxValue = BreathingSettings.MaximumSessionDurationMinutes,
            Step = BreathingSettings.SessionDurationStepMinutes,
            Value = _settings.SessionDurationMinutes,
            CustomMinimumSize = new Vector2(0, 42),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _sessionDurationSlider.ValueChanged += OnSessionDurationSliderValueChanged;
        container.AddChild(_sessionDurationSlider);

        return container;
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

        row.AddChild(CreateSettingsButton("‹", SelectPreviousTheme));

        _themeLabel = new Label
        {
            Name = "ThemeLabel",
            CustomMinimumSize = new Vector2(180, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = Godot.HorizontalAlignment.Center,
            VerticalAlignment = Godot.VerticalAlignment.Center
        };
        _themeLabel.AddThemeFontSizeOverride("font_size", 20);
        row.AddChild(_themeLabel);

        row.AddChild(CreateSettingsButton("›", SelectNextTheme));

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

    private Button CreateSettingsButton(string text, Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(112, 48)
        };
        button.AddThemeFontSizeOverride("font_size", 24);
        ApplySettingsButtonStyle(button);
        button.Pressed += onPressed;
        return button;
    }

    private Button CreateSettingsIconButton(string text, Action onPressed, int fontSize = 28)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(64, 56)
        };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        ApplySettingsButtonStyle(button);
        button.Pressed += onPressed;
        return button;
    }

    private Button CreateSettingsSaveButton(Action onPressed)
    {
        var button = new Button
        {
            Text = string.Empty,
            CustomMinimumSize = new Vector2(64, 56)
        };
        ApplySettingsButtonStyle(button);

        var icon = new FloppyIcon
        {
            Name = "SaveIcon",
            MouseFilter = MouseFilterEnum.Ignore,
            IconColor = Colors.White
        };
        button.AddChild(icon);
        // Smaller insets so the floppy icon appears larger inside the same button.
        InsetControl(icon, 7, 5, 7, 5);

        button.Pressed += onPressed;
        return button;
    }

    private static void ApplySettingsButtonStyle(Button button)
    {
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeColorOverride("font_focus_color", Colors.White);

        button.AddThemeStyleboxOverride("normal", CreateSettingsButtonStyleBox(new Color(0.04f, 0.04f, 0.04f)));
        button.AddThemeStyleboxOverride("hover", CreateSettingsButtonStyleBox(new Color(0.10f, 0.10f, 0.10f)));
        button.AddThemeStyleboxOverride("pressed", CreateSettingsButtonStyleBox(new Color(0.18f, 0.18f, 0.18f)));
        button.AddThemeStyleboxOverride("focus", CreateSettingsButtonStyleBox(new Color(0.04f, 0.04f, 0.04f)));
        button.AddThemeStyleboxOverride("disabled", CreateSettingsButtonStyleBox(new Color(0.04f, 0.04f, 0.04f)));
    }

    private static StyleBoxFlat CreateSettingsButtonStyleBox(Color fillColor)
    {
        var style = new StyleBoxFlat
        {
            BgColor = fillColor,
            BorderColor = Colors.White,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusBottomLeft = 8,
            ContentMarginLeft = 8,
            ContentMarginTop = 8,
            ContentMarginRight = 8,
            ContentMarginBottom = 8
        };

        return style;
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

    private static void InsetControl(Control control, float left, float top, float right, float bottom)
    {
        control.AnchorLeft = 0.0f;
        control.AnchorTop = 0.0f;
        control.AnchorRight = 1.0f;
        control.AnchorBottom = 1.0f;
        control.OffsetLeft = left;
        control.OffsetTop = top;
        control.OffsetRight = -right;
        control.OffsetBottom = -bottom;
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
        _draftInhaleDuration = BreathingSettings.ClampDuration(_draftInhaleDuration + delta);
        UpdateTexts();
    }

    private void AdjustExhaleDuration(double delta)
    {
        _draftExhaleDuration = BreathingSettings.ClampDuration(_draftExhaleDuration + delta);
        UpdateTexts();
    }

    private void OnSessionDurationSliderValueChanged(double value)
    {
        if (_isUpdatingSettingsControls)
        {
            return;
        }

        int minutes = BreathingSettings.ClampSessionDurationMinutes((int)Math.Round(value));
        _draftSessionDurationMinutes = minutes;

        if (_sessionDurationSlider != null && Math.Abs(_sessionDurationSlider.Value - minutes) > 0.001)
        {
            _isUpdatingSettingsControls = true;
            _sessionDurationSlider.Value = minutes;
            _isUpdatingSettingsControls = false;
        }

        UpdateTexts();
    }

    private void SelectPreviousTheme()
    {
        _draftThemeIndex = WrapThemeIndex(_draftThemeIndex - 1);
        UpdateTexts();
    }

    private void SelectNextTheme()
    {
        _draftThemeIndex = WrapThemeIndex(_draftThemeIndex + 1);
        UpdateTexts();
    }

    private void SaveSettings()
    {
        bool timingChanged =
            Math.Abs(_settings.InhaleDuration - _draftInhaleDuration) > 0.001 ||
            Math.Abs(_settings.ExhaleDuration - _draftExhaleDuration) > 0.001 ||
            _settings.SessionDurationMinutes != _draftSessionDurationMinutes;

        _settings.InhaleDuration = _draftInhaleDuration;
        _settings.ExhaleDuration = _draftExhaleDuration;
        _settings.SessionDurationMinutes = _draftSessionDurationMinutes;
        _settings.ApplyTheme(_draftThemeIndex);

        if (timingChanged)
        {
            ResetSessionProgress();
        }

        ApplyColors();
        ShowMainScreen();
    }

    private void CancelSettings()
    {
        ResetDraftSettingsFromCurrent();
        ShowMainScreen();
    }

    private void ResetDraftSettingsFromCurrent()
    {
        _draftInhaleDuration = _settings.InhaleDuration;
        _draftExhaleDuration = _settings.ExhaleDuration;
        _draftSessionDurationMinutes = _settings.SessionDurationMinutes;
        _draftThemeIndex = _settings.CurrentThemeIndex;
    }

    private static int WrapThemeIndex(int value)
    {
        int length = BreathingSettings.Themes.Length;
        if (length <= 0)
        {
            return 0;
        }

        int result = value % length;
        return result < 0 ? result + length : result;
    }

    private void StartOrResumeBreathingSession()
    {
        if (!_hasSessionStarted)
        {
            ResetSessionProgress();
        }

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
        ResetSessionProgress();
        UpdateMainScreenVisibility();
        UpdateTexts();
    }

    private void ShowSettingsScreen()
    {
        // Settings are currently available only before a breathing session starts.
        // If this is called from a future UI path, stop the session explicitly.
        _hasSessionStarted = false;
        _isRunning = false;
        ResetSessionProgress();
        ResetDraftSettingsFromCurrent();
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

    private void ResetSessionProgress()
    {
        _sessionElapsed = 0.0;
        ResetCycle();
    }

    private void ResetCycle()
    {
        _currentPhase = BreathingPhase.Inhale;
        _phaseElapsed = 0.0;
        UpdateGauge();
        UpdatePauseProgressDisplay();
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

    private double GetCurrentPhaseProgress()
    {
        return Math.Clamp(_phaseElapsed / GetCurrentPhaseDuration(), 0.0, 1.0);
    }

    private double GetSessionDuration()
    {
        return _settings.SessionDurationSeconds;
    }

    private double GetSessionProgress()
    {
        double sessionDuration = GetSessionDuration();
        return sessionDuration > 0.0
            ? Math.Clamp(_sessionElapsed / sessionDuration, 0.0, 1.0)
            : 0.0;
    }

    private static string FormatMinutesSeconds(double totalSeconds)
    {
        int seconds = Math.Max(0, (int)Math.Floor(totalSeconds));
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    private static double EaseInOut(double value)
    {
        double t = Math.Clamp(value, 0.0, 1.0);

        // Smootherstep: starts and ends with a very low speed, while keeping the
        // middle of the movement fluid. This feels more organic than a hard pause.
        return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
    }

    private void UpdateTexts()
    {
        if (_inhaleValueLabel != null)
        {
            _inhaleValueLabel.Text = $"{_draftInhaleDuration:0.0}s";
        }

        if (_exhaleValueLabel != null)
        {
            _exhaleValueLabel.Text = $"{_draftExhaleDuration:0.0}s";
        }

        if (_sessionDurationValueLabel != null)
        {
            _sessionDurationValueLabel.Text = $"{_draftSessionDurationMinutes} min";
        }

        if (_sessionDurationSlider != null &&
            Math.Abs(_sessionDurationSlider.Value - _draftSessionDurationMinutes) > 0.001)
        {
            _isUpdatingSettingsControls = true;
            _sessionDurationSlider.Value = _draftSessionDurationMinutes;
            _isUpdatingSettingsControls = false;
        }

        if (_themeLabel != null)
        {
            _themeLabel.Text = BreathingSettings.Themes[_draftThemeIndex].Name;
        }

        UpdatePauseProgressDisplay();
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

        _pauseElapsedLabel.Visible = isPausedSession;
        if (_pauseProgressBar.GetParent() is CanvasItem parentItem)
        {
            parentItem.Visible = isPausedSession;
        }

        // While running, this invisible Control catches any screen tap so the
        // user can pause without aiming for a tiny button.
        _pauseTouchArea.Visible = isRunningSession;
        _pauseTouchArea.MouseFilter = isRunningSession
            ? MouseFilterEnum.Stop
            : MouseFilterEnum.Ignore;

        UpdatePauseProgressDisplay();
    }

    private void UpdateGauge()
    {
        double phaseProgress = GetCurrentPhaseProgress();
        double easedProgress = EaseInOut(phaseProgress);

        // The drawing code expects 0 at the bottom and 1 at the top.
        // Inhale climbs from 0 to 1; exhale descends from 1 to 0.
        // Easing makes the ball slow down naturally near both ends.
        float visualProgress = _currentPhase == BreathingPhase.Inhale
            ? (float)easedProgress
            : 1.0f - (float)easedProgress;

        _gauge.SetProgress(visualProgress);
    }

    private void UpdatePauseProgressDisplay()
    {
        if (_pauseElapsedLabel == null || _pauseProgressBar == null || _pauseProgressFill == null)
        {
            return;
        }

        if (!_hasSessionStarted)
        {
            _pauseElapsedLabel.Text = string.Empty;
            _pauseProgressFill.OffsetRight = 0.0f;
            return;
        }

        double sessionDuration = GetSessionDuration();
        double sessionProgress = GetSessionProgress();

        _pauseElapsedLabel.Text = $"{FormatMinutesSeconds(_sessionElapsed)} / {FormatMinutesSeconds(sessionDuration)}";

        float fillWidth = _pauseProgressBar.Size.X * (float)sessionProgress;
        _pauseProgressFill.OffsetRight = fillWidth;
    }

    private void ApplyColors()
    {
        _background.Color = _settings.BackgroundColor;
        _gauge.GaugeColor = _settings.GaugeColor;
        _gauge.GaugeBorderColor = _settings.GaugeBorderColor;
        _gauge.BallColor = _settings.BallColor;
        _gauge.QueueRedraw();

        // Keep the breathing screen theme-aware, but leave the settings screen in
        // a neutral black-and-white style for consistent readability.
        ApplyTextColorRecursive(_mainScreen, _settings.TextColor);
        ApplyTextColorRecursive(_settingsScreen, Colors.White);
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
        else if (node is FloppyIcon floppyIcon)
        {
            floppyIcon.IconColor = color;
            floppyIcon.QueueRedraw();
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
