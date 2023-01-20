using Proj;

namespace Proj
{
	partial class Sql
	{
		static partial void PreProcess(ref string s)
		{
			var startTag = "--IGNORE-BEGIN--";
			var endTag = "--IGNORE-END--";
			var start = s.IndexOf(startTag);
			var end = s.IndexOf(endTag) + endTag.Length;
			s = s
				.Insert(start, "/*")
				.Insert(end + "/*".Length, "*/");
		}
	}
}

class Program
{
	public static void Main(string[] args)
	{
		System.Console.Write(Sql.Query);
	}
}
