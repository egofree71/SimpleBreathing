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

    // Fixed visual size used by the main start/resume/stop buttons. Keep this
    // larger than the text glyph minimum size so Godot does not expand one
    // button differently from the other based on the symbol width or height.
    private static readonly Vector2 SessionButtonSize = new(80, 68);
    private const float SessionButtonCenterY = 16.0f;
    private const float StopButtonCenterX = -92.0f;
    private const float StartResumeButtonCenterX = 0.0f;

    // Active breathing durations and color theme selection.
    private readonly BreathingSettings _settings = new();

    // Working values shown on the settings screen. They are applied and saved
    // immediately after each user change.
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

    // Margins that hold interactive content inside Android/iOS safe areas while
    // the full-screen background can still extend behind translucent system bars.
    private MarginContainer _mainScreenMargin = null!;
    private MarginContainer _settingsScreenMargin = null!;

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

    // Full-screen overlay used when a session finishes naturally.
    private Control _completionOverlay = null!;
    private ColorRect _completionFade = null!;
    private Label _completionLabel = null!;

    // Prevents multiple completion animations from being started by the same session.
    private bool _isShowingCompletion;

    // Current breathing phase, elapsed time inside that phase, and total elapsed session time.
    private BreathingPhase _currentPhase = BreathingPhase.Inhale;
    private double _phaseElapsed;
    private double _sessionElapsed;

    // A session starts only after the user presses the start button.
    private bool _hasSessionStarted;

    // False on startup: the ball is visible at the bottom, but the cycle is not moving yet.
    private bool _isRunning;

    // Remembers whether the device was already configured to keep the screen on
    // before the breathing session started, so the app can restore that state later.
    private bool _wasScreenKeptOnBeforeSession;

    // True only while this controller has explicitly enabled the keep-screen-on flag.
    private bool _isKeepingScreenOnForSession;

    /// <summary>
    /// Builds the runtime UI and initializes visuals once the scene enters the tree.
    /// </summary>
    public override void _Ready()
    {
        // Android export presets can enable immersive fullscreen mode, which hides
        // the system navigation bar. SimpleBreathing is closer to a small utility
        // app than to a game, so keep Android system bars visible by default.
        EnsureAndroidSystemBarsVisible();

        // Load persisted values before building the UI so labels, sliders, and
        // colors start with the previously saved settings on every platform.
        SettingsStorage.Load(_settings);

        BuildInterface();
        Resized += UpdateSafeAreaMargins;
        UpdateSafeAreaMargins();
        ResetDraftSettingsFromCurrent();
        ApplyColors();
        UpdateTexts();
        UpdateGauge();
        UpdatePauseProgressDisplay();
        UpdateMainScreenVisibility();
    }

    /// <summary>
    /// Keeps the Android status and navigation bars visible.
    /// </summary>
    /// <remarks>
    /// The Android export preset also has a Screen > Immersive Mode option.
    /// Disabling it there is the cleanest fix. This runtime call is a small
    /// safety net if the local export preset is still configured as fullscreen.
    /// </remarks>
    private static void EnsureAndroidSystemBarsVisible()
    {
        if (OS.GetName() != "Android")
        {
            return;
        }

        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
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
            CompleteBreathingSession();
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
    /// Restores the previous screen sleep behavior if the scene is removed during a session.
    /// </summary>
    public override void _ExitTree()
    {
        RestoreScreenKeepOnState();
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

        _completionOverlay = BuildCompletionOverlay();
        AddChild(_completionOverlay);
        FillParent(_completionOverlay);
    }

    /// <summary>
    /// Keeps interactive UI away from Android/iOS system bars and notches.
    /// </summary>
    /// <remarks>
    /// This is mainly useful when Android export option Screen > Edge to Edge is
    /// enabled: the background may draw behind translucent system bars, but the
    /// actual buttons and sliders should remain inside the safe area.
    /// </remarks>
    private void UpdateSafeAreaMargins()
    {
        if (_mainScreenMargin == null || _settingsScreenMargin == null)
        {
            return;
        }

        var safeMargins = GetSafeAreaMarginsInViewportUnits();

        ApplyMarginOverrides(
            _mainScreenMargin,
            left: 24 + safeMargins.X,
            top: 16 + safeMargins.Y,
            right: 24 + safeMargins.Z,
            bottom: 24 + safeMargins.W);

        ApplyMarginOverrides(
            _settingsScreenMargin,
            left: 24 + safeMargins.X,
            top: 24 + safeMargins.Y,
            right: 24 + safeMargins.Z,
            bottom: 24 + safeMargins.W);

        if (_topActionRow != null)
        {
            _topActionRow.Position = new Vector2(20 + safeMargins.X, 16 + safeMargins.Y);
        }
    }

    /// <summary>
    /// Converts the native display safe area from physical pixels to Godot viewport units.
    /// </summary>
    private Vector4 GetSafeAreaMarginsInViewportUnits()
    {
        var osName = OS.GetName();
        if (osName != "Android" && osName != "iOS")
        {
            return Vector4.Zero;
        }

        var viewportSize = GetViewportRect().Size;
        var windowSize = DisplayServer.WindowGetSize();
        if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f || windowSize.X <= 0 || windowSize.Y <= 0)
        {
            return Vector4.Zero;
        }

        var safeArea = DisplayServer.GetDisplaySafeArea();
        if (safeArea.Size.X <= 0 || safeArea.Size.Y <= 0)
        {
            return Vector4.Zero;
        }

        var leftPixels = Math.Max(0, safeArea.Position.X);
        var topPixels = Math.Max(0, safeArea.Position.Y);
        var rightPixels = Math.Max(0, windowSize.X - (safeArea.Position.X + safeArea.Size.X));
        var bottomPixels = Math.Max(0, windowSize.Y - (safeArea.Position.Y + safeArea.Size.Y));

        var scaleX = viewportSize.X / windowSize.X;
        var scaleY = viewportSize.Y / windowSize.Y;

        return new Vector4(
            leftPixels * scaleX,
            topPixels * scaleY,
            rightPixels * scaleX,
            bottomPixels * scaleY);
    }

    /// <summary>
    /// Applies integer margin constants to a MarginContainer.
    /// </summary>
    private static void ApplyMarginOverrides(MarginContainer margin, float left, float top, float right, float bottom)
    {
        margin.AddThemeConstantOverride("margin_left", ToMarginConstant(left));
        margin.AddThemeConstantOverride("margin_top", ToMarginConstant(top));
        margin.AddThemeConstantOverride("margin_right", ToMarginConstant(right));
        margin.AddThemeConstantOverride("margin_bottom", ToMarginConstant(bottom));
    }

    private static int ToMarginConstant(float value)
    {
        return Math.Max(0, (int)Math.Round(value));
    }

    /// <summary>
    /// Builds the full-screen overlay used to softly end a completed session.
    /// </summary>
    /// <remarks>
    /// The overlay is kept above both screens and hidden until a session reaches
    /// its configured duration. It fades the current view to black, displays the
    /// completion message briefly, then fades back to the start screen.
    /// </remarks>
    private Control BuildCompletionOverlay()
    {
        var overlay = new Control
        {
            Name = "CompletionOverlay",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop
        };

        _completionFade = new ColorRect
        {
            Name = "CompletionFade",
            Color = Colors.Black,
            MouseFilter = MouseFilterEnum.Ignore
        };
        overlay.AddChild(_completionFade);
        FillParent(_completionFade);

        var center = new CenterContainer
        {
            Name = "CompletionMessageCenter",
            MouseFilter = MouseFilterEnum.Ignore
        };
        overlay.AddChild(center);
        FillParent(center);

        _completionLabel = new Label
        {
            Name = "CompletionLabel",
            Text = AppLocalization.Translate(AppLocalization.SessionCompleted),
            HorizontalAlignment = Godot.HorizontalAlignment.Center,
            VerticalAlignment = Godot.VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _completionLabel.AddThemeFontSizeOverride("font_size", 34);
        _completionLabel.AddThemeColorOverride("font_color", Colors.White);
        center.AddChild(_completionLabel);

        _completionFade.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        _completionLabel.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);

        return overlay;
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

        _mainScreenMargin = new MarginContainer
        {
            Name = "MainScreenMargin"
        };
        root.AddChild(_mainScreenMargin);
        FillParent(_mainScreenMargin);

        var mainColumn = new VBoxContainer
        {
            Name = "MainColumn",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        mainColumn.AddThemeConstantOverride("separation", 8);
        _mainScreenMargin.AddChild(mainColumn);

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

    /// <summary>
    /// Creates the small overlay row that holds the top-left settings button.
    /// </summary>
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

    /// <summary>
    /// Creates the reserved pause display area above the gauge.
    /// </summary>
    /// <remarks>
    /// The area keeps a fixed height even when hidden, so showing the elapsed-time
    /// text and progress bar during pause does not move the gauge.
    /// </remarks>
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
        _pauseElapsedLabel.AddThemeFontSizeOverride("font_size", 24);
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

    /// <summary>
    /// Creates the fixed bottom area containing the start/resume and stop buttons.
    /// </summary>
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
        // The two buttons use the same explicit visual rectangle. This avoids a
        // subtle Godot/Button minimum-size difference where the play and stop
        // glyphs can make their backgrounds render at slightly different sizes.
        PositionSessionButton(_startResumeButton, StartResumeButtonCenterX);
        PositionSessionButton(_stopButton, StopButtonCenterX);

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

        _settingsScreenMargin = new MarginContainer
        {
            Name = "SettingsScreen"
        };
        root.AddChild(_settingsScreenMargin);
        FillParent(_settingsScreenMargin);

        var column = new VBoxContainer
        {
            Name = "SettingsColumn",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        column.AddThemeConstantOverride("separation", 18);
        _settingsScreenMargin.AddChild(column);

        var headerRow = new HBoxContainer
        {
            Name = "SettingsHeader",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        headerRow.AddThemeConstantOverride("separation", 12);
        column.AddChild(headerRow);

        headerRow.AddChild(CreateSettingsIconButton("←", ShowMainScreen, 32));

        var title = new Label
        {
            Text = AppLocalization.Translate(AppLocalization.SettingsTitle),
            VerticalAlignment = Godot.VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 28);
        headerRow.AddChild(title);

        column.AddChild(CreateVerticalSpacer(10));

        var breathingTitle = CreateSectionTitle(AppLocalization.Translate(AppLocalization.BreathingSection));
        column.AddChild(breathingTitle);

        column.AddChild(CreateDurationRow(
            AppLocalization.Translate(AppLocalization.Inhale),
            out _inhaleValueLabel,
            () => AdjustInhaleDuration(-BreathingSettings.DurationStep),
            () => AdjustInhaleDuration(BreathingSettings.DurationStep)));

        column.AddChild(CreateDurationRow(
            AppLocalization.Translate(AppLocalization.Exhale),
            out _exhaleValueLabel,
            () => AdjustExhaleDuration(-BreathingSettings.DurationStep),
            () => AdjustExhaleDuration(BreathingSettings.DurationStep)));

        column.AddChild(CreateSessionDurationSlider());

        column.AddChild(CreateVerticalSpacer(10));

        var colorsTitle = CreateSectionTitle(AppLocalization.Translate(AppLocalization.ThemesSection));
        column.AddChild(colorsTitle);
        column.AddChild(CreateThemeRow());

        // Keeps the settings content near the top without adding a separate Save button.
        // Each setting is applied and persisted immediately when the user changes it.
        var flexibleSpacer = new Control
        {
            Name = "SettingsFlexibleSpacer",
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        column.AddChild(flexibleSpacer);

        return root;
    }

    /// <summary>
    /// Creates a section title label for the settings screen.
    /// </summary>
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

    /// <summary>
    /// Creates the session-duration slider, restricted to whole-minute values.
    /// </summary>
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
            Text = AppLocalization.Translate(AppLocalization.SessionDuration),
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

    /// <summary>
    /// Creates the theme selector row with previous/next buttons and the draft theme name.
    /// </summary>
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

    /// <summary>
    /// Creates a simple default button.
    /// </summary>
    /// <remarks>
    /// Kept as a generic helper for future non-styled buttons. Most current buttons
    /// use more specialized helpers below.
    /// </remarks>
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

    /// <summary>
    /// Creates a themed icon-like button used on the main screen.
    /// </summary>
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

    /// <summary>
    /// Creates the large start/resume/stop buttons used during a breathing session.
    /// </summary>
    private Button CreateSessionIconButton(string text, Action onPressed, int fontSize)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = SessionButtonSize,
            Size = SessionButtonSize
        };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.Pressed += onPressed;
        return button;
    }

    /// <summary>
    /// Creates a black-and-white text button for the settings screen.
    /// </summary>
    private Button CreateSettingsButton(string text, Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(112, 48)
        };
        button.AddThemeFontSizeOverride("font_size", 30);
        ApplySettingsButtonStyle(button);
        button.Pressed += onPressed;
        return button;
    }

    /// <summary>
    /// Creates a black-and-white icon button for the settings header.
    /// </summary>
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

    /// <summary>
    /// Applies a soft theme-aware style to a main-screen icon button.
    /// </summary>
    /// <remarks>
    /// Godot's default Button style uses a dark grey fill. That looks acceptable
    /// on dark palettes, but becomes visually heavy on the light Sky theme. These
    /// overrides keep the buttons subtle by tinting the background from the current
    /// theme text color and using transparency instead of an opaque grey block.
    /// </remarks>
    private static void ApplyMainButtonStyle(Button button, Color iconColor, float contentShiftX = 0.0f, float contentShiftY = 0.0f)
    {
        button.AddThemeColorOverride("font_color", iconColor);
        button.AddThemeColorOverride("font_hover_color", iconColor);
        button.AddThemeColorOverride("font_pressed_color", iconColor);
        button.AddThemeColorOverride("font_focus_color", iconColor);
        button.AddThemeColorOverride("font_disabled_color", new Color(iconColor.R, iconColor.G, iconColor.B, 0.45f));

        button.AddThemeStyleboxOverride("normal", CreateMainButtonStyleBox(iconColor, 0.10f, 0.16f, contentShiftX, contentShiftY));
        button.AddThemeStyleboxOverride("hover", CreateMainButtonStyleBox(iconColor, 0.16f, 0.22f, contentShiftX, contentShiftY));
        button.AddThemeStyleboxOverride("pressed", CreateMainButtonStyleBox(iconColor, 0.24f, 0.30f, contentShiftX, contentShiftY));
        button.AddThemeStyleboxOverride("focus", CreateMainButtonStyleBox(iconColor, 0.10f, 0.32f, contentShiftX, contentShiftY));
        button.AddThemeStyleboxOverride("disabled", CreateMainButtonStyleBox(iconColor, 0.06f, 0.10f, contentShiftX, contentShiftY));
    }

    /// <summary>
    /// Creates one translucent StyleBoxFlat variant for main-screen buttons.
    /// </summary>
    private static StyleBoxFlat CreateMainButtonStyleBox(Color color, float backgroundAlpha, float borderAlpha, float contentShiftX = 0.0f, float contentShiftY = 0.0f)
    {
        const float baseContentMargin = 8.0f;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(color.R, color.G, color.B, backgroundAlpha),
            BorderColor = new Color(color.R, color.G, color.B, borderAlpha),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusBottomLeft = 8,
            ContentMarginLeft = MathF.Max(0.0f, baseContentMargin + contentShiftX),
            ContentMarginTop = MathF.Max(0.0f, baseContentMargin + contentShiftY),
            ContentMarginRight = MathF.Max(0.0f, baseContentMargin - contentShiftX),
            ContentMarginBottom = MathF.Max(0.0f, baseContentMargin - contentShiftY)
        };

        return style;
    }

    /// <summary>
    /// Applies the neutral black-and-white button style used by the settings screen.
    /// </summary>
    private static void ApplySettingsButtonStyle(Button button)
    {
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeColorOverride("font_focus_color", Colors.White);
        button.AddThemeColorOverride("font_outline_color", Colors.White);
        button.AddThemeConstantOverride("outline_size", 1);

        button.AddThemeStyleboxOverride("normal", CreateSettingsButtonStyleBox(new Color(0.04f, 0.04f, 0.04f)));
        button.AddThemeStyleboxOverride("hover", CreateSettingsButtonStyleBox(new Color(0.10f, 0.10f, 0.10f)));
        button.AddThemeStyleboxOverride("pressed", CreateSettingsButtonStyleBox(new Color(0.18f, 0.18f, 0.18f)));
        button.AddThemeStyleboxOverride("focus", CreateSettingsButtonStyleBox(new Color(0.04f, 0.04f, 0.04f)));
        button.AddThemeStyleboxOverride("disabled", CreateSettingsButtonStyleBox(new Color(0.04f, 0.04f, 0.04f)));
    }

    /// <summary>
    /// Creates one StyleBoxFlat variant for settings buttons.
    /// </summary>
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

    /// <summary>
    /// Positions a session button with a fixed visual size relative to the center of its parent.
    /// </summary>
    /// <remarks>
    /// This is used for the bottom session buttons so the resume button stays
    /// centered even when the stop button appears to its left. The fixed rectangle
    /// also prevents the play and stop glyphs from producing slightly different
    /// rendered button dimensions.
    /// </remarks>
    private static void PositionSessionButton(Control control, float centerX)
    {
        control.AnchorLeft = 0.5f;
        control.AnchorTop = 0.0f;
        control.AnchorRight = 0.5f;
        control.AnchorBottom = 0.0f;
        control.OffsetLeft = centerX - SessionButtonSize.X / 2.0f;
        control.OffsetTop = SessionButtonCenterY - SessionButtonSize.Y / 2.0f;
        control.OffsetRight = centerX + SessionButtonSize.X / 2.0f;
        control.OffsetBottom = SessionButtonCenterY + SessionButtonSize.Y / 2.0f;
        control.Size = SessionButtonSize;
    }

    /// <summary>
    /// Anchors a child control to fill its parent while keeping custom insets.
    /// </summary>
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

    /// <summary>
    /// Creates a fixed-height spacer for code-built layouts.
    /// </summary>
    private Control CreateVerticalSpacer(float height)
    {
        return new Control
        {
            CustomMinimumSize = new Vector2(0, height)
        };
    }

    /// <summary>
    /// Updates the inhalation duration and saves the change immediately.
    /// </summary>
    private void AdjustInhaleDuration(double delta)
    {
        _draftInhaleDuration = BreathingSettings.ClampDuration(_draftInhaleDuration + delta);
        ApplyCurrentSettingsImmediately();
    }

    /// <summary>
    /// Updates the exhalation duration and saves the change immediately.
    /// </summary>
    private void AdjustExhaleDuration(double delta)
    {
        _draftExhaleDuration = BreathingSettings.ClampDuration(_draftExhaleDuration + delta);
        ApplyCurrentSettingsImmediately();
    }

    /// <summary>
    /// Updates and saves the session duration when the whole-minute slider changes.
    /// </summary>
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

        ApplyCurrentSettingsImmediately();
    }

    /// <summary>
    /// Selects, applies, and saves the previous theme.
    /// </summary>
    private void SelectPreviousTheme()
    {
        _draftThemeIndex = WrapThemeIndex(_draftThemeIndex - 1);
        ApplyCurrentSettingsImmediately();
    }

    /// <summary>
    /// Selects, applies, and saves the next theme.
    /// </summary>
    private void SelectNextTheme()
    {
        _draftThemeIndex = WrapThemeIndex(_draftThemeIndex + 1);
        ApplyCurrentSettingsImmediately();
    }

    /// <summary>
    /// Copies the current settings-screen values into the active settings model and persists them.
    /// </summary>
    /// <remarks>
    /// The app uses immediate-save behavior on mobile: changing a setting applies
    /// it directly. Timing changes reset the current session progress so the gauge
    /// and progress bar remain coherent.
    /// </remarks>
    private void ApplyCurrentSettingsImmediately()
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

        SettingsStorage.Save(_settings);
        ApplyColors();
        UpdateTexts();
    }

    /// <summary>
    /// Copies active settings into the working values shown on the settings screen.
    /// </summary>
    private void ResetDraftSettingsFromCurrent()
    {
        _draftInhaleDuration = _settings.InhaleDuration;
        _draftExhaleDuration = _settings.ExhaleDuration;
        _draftSessionDurationMinutes = _settings.SessionDurationMinutes;
        _draftThemeIndex = _settings.CurrentThemeIndex;
    }

    /// <summary>
    /// Wraps a theme index so previous/next navigation loops through the theme list.
    /// </summary>
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

    /// <summary>
    /// Starts a new breathing session or resumes an existing paused one.
    /// </summary>
    private void StartOrResumeBreathingSession()
    {
        if (!_hasSessionStarted)
        {
            ResetSessionProgress();
        }

        EnableScreenKeepOnForSession();

        _hasSessionStarted = true;
        _isRunning = true;
        UpdateMainScreenVisibility();
        UpdateTexts();
    }

    /// <summary>
    /// Pauses a running session and reveals the pause controls.
    /// </summary>
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

    /// <summary>
    /// Plays the end-of-session fade animation, then returns to the start screen.
    /// </summary>
    private async void CompleteBreathingSession()
    {
        if (_isShowingCompletion)
        {
            return;
        }

        _isShowingCompletion = true;
        _isRunning = false;
        RestoreScreenKeepOnState();
        _pauseTouchArea.Visible = false;
        _pauseTouchArea.MouseFilter = MouseFilterEnum.Ignore;

        _completionOverlay.Visible = true;
        _completionOverlay.MouseFilter = MouseFilterEnum.Stop;
        _completionFade.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        _completionLabel.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);

        Tween fadeOut = CreateTween();
        fadeOut.TweenProperty(_completionFade, "modulate:a", 1.0f, 0.45f);
        await ToSignal(fadeOut, Tween.SignalName.Finished);

        // Reset the app while the screen is black, so the user does not see a
        // sudden jump from the final breathing frame back to the start state.
        _hasSessionStarted = false;
        ResetSessionProgress();
        UpdateMainScreenVisibility();
        UpdateTexts();

        Tween textFadeIn = CreateTween();
        textFadeIn.TweenProperty(_completionLabel, "modulate:a", 1.0f, 0.35f);
        await ToSignal(textFadeIn, Tween.SignalName.Finished);

        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

        Tween fadeIn = CreateTween();
        fadeIn.SetParallel(true);
        fadeIn.TweenProperty(_completionLabel, "modulate:a", 0.0f, 0.35f);
        fadeIn.TweenProperty(_completionFade, "modulate:a", 0.0f, 0.45f);
        await ToSignal(fadeIn, Tween.SignalName.Finished);

        _completionOverlay.Visible = false;
        _completionOverlay.MouseFilter = MouseFilterEnum.Ignore;
        _isShowingCompletion = false;
    }

    /// <summary>
    /// Stops the current session and returns the main screen to its initial state.
    /// </summary>
    private void StopBreathingSession()
    {
        _hasSessionStarted = false;
        _isRunning = false;
        RestoreScreenKeepOnState();
        ResetSessionProgress();
        UpdateMainScreenVisibility();
        UpdateTexts();
    }

    /// <summary>
    /// Opens the settings screen and initializes its controls from the active settings.
    /// </summary>
    private void ShowSettingsScreen()
    {
        // Settings are currently available only before a breathing session starts.
        // If this is called from a future UI path, stop the session explicitly.
        _hasSessionStarted = false;
        _isRunning = false;
        RestoreScreenKeepOnState();
        ResetSessionProgress();
        ResetDraftSettingsFromCurrent();
        UpdateMainScreenVisibility();
        UpdateTexts();

        _mainScreen.Visible = false;
        _settingsScreen.Visible = true;
    }

    /// <summary>
    /// Returns from settings to the main breathing screen.
    /// </summary>
    private void ShowMainScreen()
    {
        _settingsScreen.Visible = false;
        _mainScreen.Visible = true;
        UpdateMainScreenVisibility();
        UpdateTexts();
    }

    /// <summary>
    /// Prevents the phone screen from sleeping while a breathing session is active.
    /// </summary>
    private void EnableScreenKeepOnForSession()
    {
        if (_isKeepingScreenOnForSession)
        {
            return;
        }

        _wasScreenKeptOnBeforeSession = DisplayServer.ScreenIsKeptOn();
        DisplayServer.ScreenSetKeepOn(true);
        _isKeepingScreenOnForSession = true;
    }

    /// <summary>
    /// Restores the screen sleep behavior that was active before the session started.
    /// </summary>
    private void RestoreScreenKeepOnState()
    {
        if (!_isKeepingScreenOnForSession)
        {
            return;
        }

        DisplayServer.ScreenSetKeepOn(_wasScreenKeptOnBeforeSession);
        _isKeepingScreenOnForSession = false;
    }

    /// <summary>
    /// Resets both the total session timer and the current breathing cycle.
    /// </summary>
    private void ResetSessionProgress()
    {
        _sessionElapsed = 0.0;
        ResetCycle();
    }

    /// <summary>
    /// Resets the inhalation/exhalation cycle to the initial inhale phase.
    /// </summary>
    private void ResetCycle()
    {
        _currentPhase = BreathingPhase.Inhale;
        _phaseElapsed = 0.0;
        UpdateGauge();
        UpdatePauseProgressDisplay();
        UpdateTexts();
    }

    /// <summary>
    /// Handles taps/clicks on the transparent full-screen pause area.
    /// </summary>
    private void OnPauseTouchAreaGuiInput(InputEvent inputEvent)
    {
        if (!IsPrimaryPress(inputEvent))
        {
            return;
        }

        PauseBreathingSession();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>
    /// Returns true for a primary touch or left mouse-button press.
    /// </summary>
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

    /// <summary>
    /// Gets the active duration for the current inhale or exhale phase.
    /// </summary>
    private double GetCurrentPhaseDuration()
    {
        return _currentPhase == BreathingPhase.Inhale
            ? _settings.InhaleDuration
            : _settings.ExhaleDuration;
    }

    /// <summary>
    /// Gets normalized progress inside the current breathing phase.
    /// </summary>
    private double GetCurrentPhaseProgress()
    {
        return Math.Clamp(_phaseElapsed / GetCurrentPhaseDuration(), 0.0, 1.0);
    }

    /// <summary>
    /// Gets the total configured session duration in seconds.
    /// </summary>
    private double GetSessionDuration()
    {
        return _settings.SessionDurationSeconds;
    }

    /// <summary>
    /// Gets normalized progress through the whole breathing session.
    /// </summary>
    private double GetSessionProgress()
    {
        double sessionDuration = GetSessionDuration();
        return sessionDuration > 0.0
            ? Math.Clamp(_sessionElapsed / sessionDuration, 0.0, 1.0)
            : 0.0;
    }

    /// <summary>
    /// Formats a duration as minutes and seconds for the pause progress display.
    /// </summary>
    private static string FormatMinutesSeconds(double totalSeconds)
    {
        int seconds = Math.Max(0, (int)Math.Floor(totalSeconds));
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    /// <summary>
    /// Applies smootherstep easing to make the ball slow down near both ends.
    /// </summary>
    private static double EaseInOut(double value)
    {
        double t = Math.Clamp(value, 0.0, 1.0);

        // Smootherstep: starts and ends with a very low speed, while keeping the
        // middle of the movement fluid. This feels more organic than a hard pause.
        return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
    }

    /// <summary>
    /// Refreshes all labels and slider values from the current draft/session state.
    /// </summary>
    private void UpdateTexts()
    {
        if (_completionLabel != null)
        {
            _completionLabel.Text = AppLocalization.Translate(AppLocalization.SessionCompleted);
        }

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
            _sessionDurationValueLabel.Text = AppLocalization.FormatSessionDurationMinutes(_draftSessionDurationMinutes);
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
            _themeLabel.Text = AppLocalization.Translate(BreathingSettings.Themes[_draftThemeIndex].NameKey);
        }

        UpdatePauseProgressDisplay();
    }

    /// <summary>
    /// Shows or hides main-screen controls according to the current session state.
    /// </summary>
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

    /// <summary>
    /// Converts current phase progress into eased gauge progress and moves the ball.
    /// </summary>
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

    /// <summary>
    /// Refreshes the pause-only elapsed-time label and progress bar fill.
    /// </summary>
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

    /// <summary>
    /// Applies the active theme to the breathing screen and keeps settings neutral.
    /// </summary>
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

        ApplyMainButtonStyle(_settingsButton, _settings.TextColor);
        ApplyMainButtonStyle(_startResumeButton, _settings.TextColor, contentShiftX: 2.0f);
        ApplyMainButtonStyle(_stopButton, _settings.TextColor);
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
