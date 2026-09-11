partial class Program
{
	public static void Main(string[] args)
	{
		var lang = SetCulture();
		System.Console.Write(lang + " " + Proj.Strings.Message);
	}
}
