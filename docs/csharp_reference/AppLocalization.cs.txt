using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Small application localization helper.
/// </summary>
/// <remarks>
/// The app currently has only a few UI strings, so a lightweight in-code table is
/// simpler than setting up imported Godot translation resources. The selected
/// language follows the device/application locale reported by Godot, which works
/// on Android without any special permission.
/// </remarks>
public static class AppLocalization
{
    public const string SettingsTitle = "SETTINGS_TITLE";
    public const string BreathingSection = "BREATHING_SECTION";
    public const string Inhale = "INHALE";
    public const string Exhale = "EXHALE";
    public const string SessionDuration = "SESSION_DURATION";
    public const string ThemesSection = "THEMES_SECTION";
    public const string SessionCompleted = "SESSION_COMPLETED";
    public const string SessionDurationMinutesFormat = "SESSION_DURATION_MINUTES_FORMAT";

    public const string ThemeOcean = "THEME_OCEAN";
    public const string ThemeJungle = "THEME_JUNGLE";
    public const string ThemeVolcano = "THEME_VOLCANO";
    public const string ThemeSky = "THEME_SKY";

    private const string FallbackLanguage = "en";

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["en"] = new Dictionary<string, string>
        {
            [SettingsTitle] = "Settings",
            [BreathingSection] = "Breathing",
            [Inhale] = "Inhale",
            [Exhale] = "Exhale",
            [SessionDuration] = "Session duration",
            [ThemesSection] = "Themes",
            [SessionCompleted] = "Session completed",
            [SessionDurationMinutesFormat] = "{0} min",
            [ThemeOcean] = "Ocean",
            [ThemeJungle] = "Jungle",
            [ThemeVolcano] = "Volcano",
            [ThemeSky] = "Sky"
        },

        ["fr"] = new Dictionary<string, string>
        {
            [SettingsTitle] = "Réglages",
            [BreathingSection] = "Respiration",
            [Inhale] = "Inspiration",
            [Exhale] = "Expiration",
            [SessionDuration] = "Durée de séance",
            [ThemesSection] = "Thèmes",
            [SessionCompleted] = "Session terminée",
            [SessionDurationMinutesFormat] = "{0} min",
            [ThemeOcean] = "Océan",
            [ThemeJungle] = "Jungle",
            [ThemeVolcano] = "Volcan",
            [ThemeSky] = "Ciel"
        },

        ["es"] = new Dictionary<string, string>
        {
            [SettingsTitle] = "Ajustes",
            [BreathingSection] = "Respiración",
            [Inhale] = "Inspiración",
            [Exhale] = "Exhalación",
            [SessionDuration] = "Duración de la sesión",
            [ThemesSection] = "Temas",
            [SessionCompleted] = "Sesión terminada",
            [SessionDurationMinutesFormat] = "{0} min",
            [ThemeOcean] = "Océano",
            [ThemeJungle] = "Jungla",
            [ThemeVolcano] = "Volcán",
            [ThemeSky] = "Cielo"
        }
    };

    /// <summary>
    /// Returns the localized text for a key, falling back to English and then to the key itself.
    /// </summary>
    public static string Translate(string key)
    {
        string language = GetLanguageCode();

        if (TryGetTranslation(language, key, out string? text))
        {
            return text;
        }

        if (TryGetTranslation(FallbackLanguage, key, out text))
        {
            return text;
        }

        return key;
    }

    /// <summary>
    /// Formats a whole-minute session duration with the current language.
    /// </summary>
    public static string FormatSessionDurationMinutes(int minutes)
    {
        return string.Format(Translate(SessionDurationMinutesFormat), minutes);
    }

    private static bool TryGetTranslation(string language, string key, out string? text)
    {
        text = null;
        return Translations.TryGetValue(language, out Dictionary<string, string>? languageTable) &&
               languageTable.TryGetValue(key, out text);
    }

    private static string GetLanguageCode()
    {
        string locale = TranslationServer.GetLocale();
        if (string.IsNullOrWhiteSpace(locale))
        {
            return FallbackLanguage;
        }

        string normalizedLocale = locale.Replace('-', '_').ToLowerInvariant();

        if (normalizedLocale.StartsWith("fr", StringComparison.Ordinal))
        {
            return "fr";
        }

        if (normalizedLocale.StartsWith("es", StringComparison.Ordinal))
        {
            return "es";
        }

        return FallbackLanguage;
    }
}
