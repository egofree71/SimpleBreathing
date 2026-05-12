extends RefCounted

## Small application localization helper.
##
## The app currently has only a few UI strings, so a lightweight in-code table is
## simpler than setting up imported Godot translation resources. The selected
## language follows the device/application locale reported by Godot.

const SETTINGS_TITLE := "SETTINGS_TITLE"
const BREATHING_SECTION := "BREATHING_SECTION"
const INHALE := "INHALE"
const EXHALE := "EXHALE"
const SESSION_DURATION := "SESSION_DURATION"
const THEMES_SECTION := "THEMES_SECTION"
const SESSION_COMPLETED := "SESSION_COMPLETED"
const SESSION_DURATION_MINUTES_FORMAT := "SESSION_DURATION_MINUTES_FORMAT"

const THEME_OCEAN := "THEME_OCEAN"
const THEME_JUNGLE := "THEME_JUNGLE"
const THEME_VOLCANO := "THEME_VOLCANO"
const THEME_SKY := "THEME_SKY"

const FALLBACK_LANGUAGE := "en"

const TRANSLATIONS := {
	"en": {
		SETTINGS_TITLE: "Settings",
		BREATHING_SECTION: "Breathing",
		INHALE: "Inhale",
		EXHALE: "Exhale",
		SESSION_DURATION: "Session duration",
		THEMES_SECTION: "Themes",
		SESSION_COMPLETED: "Session completed",
		SESSION_DURATION_MINUTES_FORMAT: "{0} min",
		THEME_OCEAN: "Ocean",
		THEME_JUNGLE: "Jungle",
		THEME_VOLCANO: "Volcano",
		THEME_SKY: "Sky",
	},
	"fr": {
		SETTINGS_TITLE: "Réglages",
		BREATHING_SECTION: "Respiration",
		INHALE: "Inspiration",
		EXHALE: "Expiration",
		SESSION_DURATION: "Durée de séance",
		THEMES_SECTION: "Thèmes",
		SESSION_COMPLETED: "Session terminée",
		SESSION_DURATION_MINUTES_FORMAT: "{0} min",
		THEME_OCEAN: "Océan",
		THEME_JUNGLE: "Jungle",
		THEME_VOLCANO: "Volcan",
		THEME_SKY: "Ciel",
	},
	"es": {
		SETTINGS_TITLE: "Ajustes",
		BREATHING_SECTION: "Respiración",
		INHALE: "Inspiración",
		EXHALE: "Exhalación",
		SESSION_DURATION: "Duración de la sesión",
		THEMES_SECTION: "Temas",
		SESSION_COMPLETED: "Sesión terminada",
		SESSION_DURATION_MINUTES_FORMAT: "{0} min",
		THEME_OCEAN: "Océano",
		THEME_JUNGLE: "Jungla",
		THEME_VOLCANO: "Volcán",
		THEME_SKY: "Cielo",
	},
}


## Returns the translation for a key using the device language, then English as
## fallback. Unknown keys are returned as-is to make missing entries visible.
static func translate(key: String) -> String:
	var language := _get_language_code()

	if TRANSLATIONS.has(language) and TRANSLATIONS[language].has(key):
		return TRANSLATIONS[language][key]

	if TRANSLATIONS.has(FALLBACK_LANGUAGE) and TRANSLATIONS[FALLBACK_LANGUAGE].has(key):
		return TRANSLATIONS[FALLBACK_LANGUAGE][key]

	return key


static func format_session_duration_minutes(minutes: int) -> String:
	return translate(SESSION_DURATION_MINUTES_FORMAT).format([minutes])


## Maps full locales such as fr_CH or es_ES to the small set of languages the
## app currently supports.
static func _get_language_code() -> String:
	var locale := TranslationServer.get_locale()
	if locale.strip_edges().is_empty():
		return FALLBACK_LANGUAGE

	var normalized_locale := locale.replace("-", "_").to_lower()

	if normalized_locale.begins_with("fr"):
		return "fr"

	if normalized_locale.begins_with("es"):
		return "es"

	return FALLBACK_LANGUAGE
