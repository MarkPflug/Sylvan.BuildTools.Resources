
partial class Program
{
	static string SetCulture(string? lang = null)
	{
		if (lang == null)
		{
			var args = System.Environment.GetCommandLineArgs();
			// Env args include the dll as the first argument.
			lang = args.Length > 1 ? args[1] : "en-US";
		}

		var culture = System.Globalization.CultureInfo.GetCultureInfoByIetfLanguageTag(lang);
		System.Threading.Thread.CurrentThread.CurrentCulture = culture;
		System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
		return culture.Name;
	}
}
