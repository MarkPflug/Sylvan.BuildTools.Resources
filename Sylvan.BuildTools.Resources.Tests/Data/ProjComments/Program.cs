class Program
{
    public static void Main(string[] args)
    {
        var lang = args.Length > 0 ? args[0] : "en-US";
        var culture = System.Globalization.CultureInfo.GetCultureInfoByIetfLanguageTag(lang);
        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
        System.Console.WriteLine(Proj.Strings.Message);
    }
}
