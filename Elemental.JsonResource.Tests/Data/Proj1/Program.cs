class Program
{
    public static void Main(string[] args)
    {
        var lang = args.Length > 0 ? args[0] : "en-US";
        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfoByIetfLanguageTag(lang);
        System.Console.WriteLine(Strings.Message);
    }
}
