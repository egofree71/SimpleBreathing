using Godot;

/// <summary>
/// Loads and saves breathing settings in Godot's user data folder.
/// </summary>
/// <remarks>
/// The path uses <c>user://</c>, which is the portable Godot way to store app
/// data. On Android this resolves to the app's private writable storage, so it
/// works without requesting external storage permissions.
/// </remarks>
public static class SettingsStorage
{
    private const string SettingsPath = "user://settings.cfg";
    private const string SettingsSection = "breathing";

    private const string InhaleDurationKey = "inhale_duration";
    private const string ExhaleDurationKey = "exhale_duration";
    private const string SessionDurationMinutesKey = "session_duration_minutes";
    private const string ThemeIndexKey = "theme_index";

    /// <summary>
    /// Loads saved settings into the provided model.
    /// </summary>
    /// <returns>
    /// <c>true</c> if a settings file was found and loaded, otherwise <c>false</c>.
    /// </returns>
    public static bool Load(BreathingSettings settings)
    {
        if (!FileAccess.FileExists(SettingsPath))
        {
            return false;
        }

        var config = new ConfigFile();
        Error error = config.Load(SettingsPath);

        if (error != Error.Ok)
        {
            GD.PushWarning($"Could not load settings from {SettingsPath}: {error}");
            return false;
        }

        settings.InhaleDuration = BreathingSettings.ClampDuration(
            ReadDouble(config, InhaleDurationKey, settings.InhaleDuration));

        settings.ExhaleDuration = BreathingSettings.ClampDuration(
            ReadDouble(config, ExhaleDurationKey, settings.ExhaleDuration));

        settings.SessionDurationMinutes = BreathingSettings.ClampSessionDurationMinutes(
            ReadInt(config, SessionDurationMinutesKey, settings.SessionDurationMinutes));

        settings.ApplyTheme(ReadInt(config, ThemeIndexKey, settings.CurrentThemeIndex));

        return true;
    }

    /// <summary>
    /// Saves the provided settings to the app's writable user data folder.
    /// </summary>
    public static bool Save(BreathingSettings settings)
    {
        var config = new ConfigFile();

        config.SetValue(SettingsSection, InhaleDurationKey, settings.InhaleDuration);
        config.SetValue(SettingsSection, ExhaleDurationKey, settings.ExhaleDuration);
        config.SetValue(SettingsSection, SessionDurationMinutesKey, settings.SessionDurationMinutes);
        config.SetValue(SettingsSection, ThemeIndexKey, settings.CurrentThemeIndex);

        Error error = config.Save(SettingsPath);

        if (error != Error.Ok)
        {
            GD.PushWarning($"Could not save settings to {SettingsPath}: {error}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads a double value, keeping the provided default if the key is missing.
    /// </summary>
    private static double ReadDouble(ConfigFile config, string key, double defaultValue)
    {
        if (!config.HasSectionKey(SettingsSection, key))
        {
            return defaultValue;
        }

        return (double)config.GetValue(SettingsSection, key, defaultValue);
    }

    /// <summary>
    /// Reads an integer value, keeping the provided default if the key is missing.
    /// </summary>
    private static int ReadInt(ConfigFile config, string key, int defaultValue)
    {
        if (!config.HasSectionKey(SettingsSection, key))
        {
            return defaultValue;
        }

        return (int)config.GetValue(SettingsSection, key, defaultValue);
    }
}
