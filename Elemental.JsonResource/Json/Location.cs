namespace Elemental.Json
{
	public struct Location
	{
		public static Location Empty { get => default; }

		public Location(int line, int column)
		{
			this.Line = line;
			this.Column = column;
		}

		public int Line { get; }
		public int Column { get; }

		public override string ToString()
		{
			return Line.ToString() + "," + Column.ToString();
		}
	}
}
