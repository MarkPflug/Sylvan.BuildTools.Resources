partial class Program
{
    public static void Main(string[] args)
    {
		SetCulture();
        System.Console.Write(Proj.Strings.Name + " " + Proj.A.Strings.Name + " " + Proj.A.B.Strings.Name);
    }
}
