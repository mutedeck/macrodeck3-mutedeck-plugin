using MacroDeck.Localization;

namespace MuteDeckPlugin;

/// <summary>
/// Resolves one of the plugin's own strings in the catalog's default language, for the few places the
/// SDK takes a plain string: variable values, notifications and migrated action labels. The text still
/// lives in Strings.resx, so it is reviewed and translated with everything else.
/// </summary>
internal static class DefaultCultureText
{
	public static string Of(LocalizedText text)
	{
		if (text.Localized is not { } localized)
		{
			return text.Literal ?? string.Empty;
		}

		var catalog = Strings.LocalizationCatalog;
		if (!catalog.TryGetTemplate(catalog.DefaultCulture, localized.Key.Name, out var template))
		{
			return string.Empty;
		}

		foreach (var (name, value) in localized.Arguments)
		{
			template = template.Replace("{" + name + "}", Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
				StringComparison.Ordinal);
		}

		return template;
	}
}
