using Proj;

namespace Proj
{
	partial class Sql
	{
		static partial void PreProcess(ref string s)
		{
			s = s.Replace("my_table", "your_table");
		}
	}
}

class Program
{
	public static void Main(string[] args)
	{
		System.Console.Write(Proj.Sql.Query);
	}
}
